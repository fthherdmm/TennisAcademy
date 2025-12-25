using KirikkaleTenisAkademi.Application.DTOs;
using KirikkaleTenisAkademi.Domain.Entities;
using KirikkaleTenisAkademi.Domain.Enums;
using KirikkaleTenisAkademi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace KirikkaleTenisAkademi.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Sadece giriş yapmış kullanıcılar
    public class LessonController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public LessonController(AppDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpPost("book")]
        public async Task<IActionResult> BookLesson([FromBody] BookLessonRequest request)
        {
            // ==============================================================================
            // 1. KRİTİK DÜZELTME: SAATLERİ EN BAŞTA UTC'YE ÇEVİR VE SABİTLE
            // ==============================================================================
            // Veritabanı UTC çalıştığı için, karşılaştırma yapacağımız saatleri de UTC yapmalıyız.
            DateTime requestedStartUtc = request.StartTime.ToUniversalTime();
            DateTime requestedEndUtc = request.EndTime.ToUniversalTime();

            // 2. KULLANICIYI BUL
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var student = await _userManager.FindByIdAsync(userId);

            if (student == null) return Unauthorized("Kullanıcı bulunamadı.");

            // 3. KREDİ KONTROLÜ
            if (student.LessonCredits < 1)
            {
                return BadRequest("Yetersiz bakiye! Lütfen önce paket satın alınız.");
            }

            // 4. MÜSAİTLİK KONTROLÜ
            
            // A. Koçun o saatte başka bir DERSİ var mı? (UTC değişkenleri kullanıyoruz)
            var isCoachBusy = await _context.LessonBookings
                .AnyAsync(b => b.CoachId == request.CoachId 
                            && b.Status != BookingStatus.Cancelled
                            && b.Status != BookingStatus.Rejected
                            && b.StartTime < requestedEndUtc   // Düzeltildi
                            && b.EndTime > requestedStartUtc); // Düzeltildi

            if (isCoachBusy) return BadRequest("Bu saatte eğitmenin zaten bir rezervasyonu var.");

            // B. Hoca o saati KAPATMIŞ mı? (UTC değişkenleri kullanıyoruz)
            var isBlocked = await _context.CoachUnavailabilities
                .AnyAsync(u => u.CoachId == request.CoachId
                            && u.StartTime < requestedEndUtc   // Düzeltildi
                            && u.EndTime > requestedStartUtc); // Düzeltildi

            if (isBlocked) return BadRequest("Eğitmen bu saat aralığında müsait değil.");

            // C. ÖĞRENCİNİN O SAATTE BAŞKA DERSİ VAR MI? (UTC değişkenleri kullanıyoruz)
            var isStudentBusy = await _context.LessonBookings
                .AnyAsync(b => b.StudentId == userId 
                            && b.Status != BookingStatus.Cancelled
                            && b.Status != BookingStatus.Rejected 
                            && b.StartTime < requestedEndUtc    // Düzeltildi
                            && b.EndTime > requestedStartUtc);  // Düzeltildi

            if (isStudentBusy) 
            {
                return BadRequest("Bu saat aralığında zaten başka bir dersiniz var. Aynı saate iki ders alamazsınız.");
            }

            // 5. İŞLEMİ YAP
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                student.LessonCredits -= 1;
                await _userManager.UpdateAsync(student);

                var booking = new LessonBooking
                {
                    StudentId = student.Id,
                    CoachId = request.CoachId,
                    // Yukarıda sabitlediğimiz UTC saatleri kullanıyoruz
                    StartTime = requestedStartUtc, 
                    EndTime = requestedEndUtc,
                    Status = BookingStatus.Confirmed,
                    CreatedDate = DateTime.UtcNow
                };

                _context.LessonBookings.Add(booking);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { message = "Rezervasyon başarıyla oluşturuldu.", remainingCredits = student.LessonCredits });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, "Rezervasyon sırasında bir hata oluştu: " + ex.Message);
            }
        }
        
        // ==========================================
        // DERSLERİM (Öğrenci İçin)
        // ==========================================
        [HttpGet("my-bookings")]
        public async Task<IActionResult> GetMyBookings()
        {
            // Giriş yapan kullanıcının ID'sini bul
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            var bookings = await _context.LessonBookings
                .Include(b => b.Coach) // Hoca bilgilerini de çek (İsim lazım)
                .Where(b => b.StudentId == userId) // Sadece benim derslerim
                .OrderByDescending(b => b.StartTime) // En yakındaki en üstte
                .Select(b => new 
                {
                    Id = b.Id,
                    // Hoca ismini Coach tablosundan alıyoruz
                    CoachName = b.Coach.FirstName + " " + b.Coach.LastName, 
                    StartTime = b.StartTime,
                    EndTime = b.EndTime,
                    Status = b.Status.ToString()
                })
                .ToListAsync();

            return Ok(bookings);
        }
        
        [HttpPut("cancel/{bookingId}")]
        [Authorize(Roles = "Coach,Admin")] // Sadece Koç ve Admin yapabilir
        public async Task<IActionResult> CancelLesson(int bookingId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // 1. Dersi Bul
            var booking = await _context.LessonBookings
                .Include(b => b.Coach)
                .FirstOrDefaultAsync(b => b.Id == bookingId);

            if (booking == null) return NotFound("Ders bulunamadı.");

            // Güvenlik: Başka hocanın dersini iptal edemesin (Admin değilse)
            if (!User.IsInRole("Admin") && booking.Coach.AppUserId != userId)
                return Unauthorized("Bu işlem için yetkiniz yok.");

            // Zaten iptal edilmişse dur
            if (booking.Status == BookingStatus.Cancelled)
                return BadRequest("Ders zaten iptal edilmiş.");

            // 2. TRANSACTION BAŞLAT (Para işi şakaya gelmez)
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // A. Durumu Güncelle
                booking.Status = BookingStatus.Cancelled;

                // B. Öğrenciye İade Yap
                var student = await _userManager.FindByIdAsync(booking.StudentId);
                if (student != null)
                {
                    student.LessonCredits += 1; // 1 Kredi İade
                    await _userManager.UpdateAsync(student);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { message = "Ders iptal edildi ve 1 kredi öğrenciye iade edildi." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, "İptal işlemi sırasında hata oluştu: " + ex.Message);
            }
        }
        
        // ==========================================
        // ÖĞRENCİ KENDİ DERSİNİ İPTAL ETME (YENİ)
        // ==========================================
        [HttpPut("cancel-my-booking/{bookingId}")]
        public async Task<IActionResult> CancelMyBooking(int bookingId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // 1. Dersi Bul
            var booking = await _context.LessonBookings
                .FirstOrDefaultAsync(b => b.Id == bookingId);

            if (booking == null) return NotFound("Ders bulunamadı.");

            // 2. GÜVENLİK KONTROLÜ: Ders gerçekten bu öğrencinin mi?
            if (booking.StudentId != userId)
                return Unauthorized("Bu dersi iptal etme yetkiniz yok.");

            // 3. Geçmiş ders iptal edilemez
            if (booking.StartTime < DateTime.UtcNow)
                return BadRequest("Geçmiş tarihli dersler iptal edilemez.");

            // 4. Zaten iptal edilmişse dur
            if (booking.Status == BookingStatus.Cancelled)
                return BadRequest("Ders zaten iptal edilmiş.");

            // 5. İŞLEM (Transaction)
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // A. Durumu İptal Yap
                booking.Status = BookingStatus.Cancelled;

                // B. Öğrenciye Kredisini İADE ET
                var student = await _userManager.FindByIdAsync(userId);
                if (student != null)
                {
                    student.LessonCredits += 1; 
                    await _userManager.UpdateAsync(student);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { message = "Dersiniz iptal edildi ve 1 kredi hesabınıza iade edildi." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, "İptal işlemi sırasında hata oluştu.");
            }
        }
    }
}