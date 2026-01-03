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

        // ==========================================
        // DERS REZERVASYONU (BASİTLEŞTİRİLMİŞ)
        // ==========================================
        [HttpPost("book")]
        public async Task<IActionResult> BookLesson([FromBody] BookLessonRequest request)
        {
            // 1. Zaman Ayarı (UTC)
            DateTime startUtc = request.StartTime.ToUniversalTime();
            DateTime endUtc = request.EndTime.ToUniversalTime();

            // ==============================================================================
            // 🛑 YENİ KURAL: 12 SAAT ÖNCESİNDEN REZERVASYON YAPILAMAZ
            // ==============================================================================
            if (startUtc < DateTime.UtcNow.AddHours(12))
            {
                return BadRequest("Ders saatine 12 saatten az kaldığı için yeni rezervasyon oluşturulamaz. Lütfen daha ileri bir tarih seçiniz.");
            }
            // ==============================================================================

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var student = await _userManager.FindByIdAsync(userId);
            if (student == null) return Unauthorized("Kullanıcı bulunamadı.");

            // 2. Müsaitlik Kontrolü (Standart Çakışma Kontrolleri)
            var isBlocked = await _context.CoachUnavailabilities
                .AnyAsync(u => u.CoachId == request.CoachId && (u.StartTime < endUtc && u.EndTime > startUtc));

            var hasPrivateLesson = await _context.LessonBookings
                .AnyAsync(b => b.CoachId == request.CoachId && 
                               b.Status != BookingStatus.Cancelled && 
                               b.Status != BookingStatus.Rejected && 
                               (b.StartTime < endUtc && b.EndTime > startUtc));

            var hasGroupLesson = await _context.GroupLessons
                .AnyAsync(g => g.CoachId == request.CoachId && 
                               g.IsActive && 
                               (g.StartTime < endUtc && g.EndTime > startUtc));

            if (isBlocked || hasPrivateLesson || hasGroupLesson)
                return BadRequest("Eğitmen bu saatte müsait değil.");

            // ==============================================================================
            // ⚠️ DERS BÜTÜNLÜĞÜ KURALI (2 SAAT KURALI)
            // ==============================================================================
            
            var dayStart = startUtc.Date;
            var dayEnd = dayStart.AddDays(1);

            var privateEndTimes = await _context.LessonBookings
                .Where(b => b.CoachId == request.CoachId && 
                            b.Status != BookingStatus.Cancelled && 
                            b.Status != BookingStatus.Rejected &&
                            b.EndTime > dayStart && b.StartTime < dayEnd)
                .Select(b => b.EndTime)
                .ToListAsync();

            var groupEndTimes = await _context.GroupLessons
                .Where(g => g.CoachId == request.CoachId && 
                            g.IsActive && 
                            g.EndTime > dayStart && g.StartTime < dayEnd)
                .Select(g => g.EndTime)
                .ToListAsync();

            var allEndTimes = privateEndTimes.Concat(groupEndTimes).ToList();

            var previousLessonEndTime = allEndTimes
                .Where(t => t <= startUtc) 
                .OrderByDescending(t => t)
                .FirstOrDefault();

            if (previousLessonEndTime != default(DateTime))
            {
                var gap = startUtc - previousLessonEndTime;
                if (gap.TotalHours > 2)
                {
                    var prevLocal = previousLessonEndTime.AddHours(3).ToString("HH:mm");
                    return BadRequest($"Ders bütünlüğü kuralı: Saat {prevLocal}'da biten dersten sonra en fazla 2 saat boşluk bırakabilirsiniz.");
                }
            }

            // 3. KAYIT VE KREDİ İŞLEMLERİ (Transaction)
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // SENARYO A: GRUP
                if (request.LessonType == "Group")
                {
                    if (student.GroupCredits < 1)
                        return BadRequest("Yetersiz 'Grup Dersi' kredisi!");

                    student.GroupCredits -= 1;
                    await _userManager.UpdateAsync(student);

                    var groupLesson = new GroupLesson
                    {
                        CoachId = request.CoachId,
                        Title = $"{student.FirstName} {student.LastName} (Grup)",
                        Description = "Öğrenci tarafından oluşturulan grup rezervasyonu.",
                        StartTime = startUtc,
                        EndTime = endUtc,
                        Capacity = 4,
                        CreditCost = 1,
                        MinLevel = TennisLevel.Beginner,
                        IsActive = true
                    };
                    
                    _context.GroupLessons.Add(groupLesson);
                    await _context.SaveChangesAsync();

                    var registration = new GroupLessonRegistration
                    {
                        GroupLessonId = groupLesson.Id,
                        StudentId = student.Id,
                        RegistrationDate = DateTime.UtcNow // Düzeltildi
                    };
                    _context.GroupLessonRegistrations.Add(registration);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Ok(new { message = "Grup dersi rezervasyonunuz onaylandı.", remainingCredits = student.GroupCredits });
                }
                // SENARYO B: BİREYSEL
                else 
                {
                    if (student.LessonCredits < 1)
                        return BadRequest("Yetersiz 'Özel Ders' kredisi!");

                    student.LessonCredits -= 1; 
                    await _userManager.UpdateAsync(student);

                    var booking = new LessonBooking
                    {
                        StudentId = student.Id,
                        CoachId = request.CoachId,
                        StartTime = startUtc, 
                        EndTime = endUtc,
                        Status = BookingStatus.Confirmed,
                        CreatedDate = DateTime.UtcNow
                    };

                    _context.LessonBookings.Add(booking);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Ok(new { message = "Özel ders rezervasyonunuz onaylandı.", remainingCredits = student.LessonCredits });
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, "İşlem hatası: " + ex.Message);
            }
        }

        // ==========================================
        // DERSLERİM
        // ==========================================
        [HttpGet("my-bookings")]
        public async Task<IActionResult> GetMyBookings()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // 1. Bireysel Dersler
            var privateLessons = await _context.LessonBookings
                .Include(b => b.Coach)
                .Where(b => b.StudentId == userId && b.Status != BookingStatus.Cancelled)
                .Select(b => new 
                {
                    Id = b.Id,
                    CoachName = b.Coach.FirstName + " " + b.Coach.LastName, 
                    StartTime = b.StartTime,
                    EndTime = b.EndTime,
                    Status = b.Status.ToString(),
                    LessonType = "Private"
                })
                .ToListAsync();

            // 2. Grup Dersleri
            var groupLessons = await _context.GroupLessonRegistrations
                .Include(r => r.GroupLesson)
                .ThenInclude(gl => gl.Coach)
                .Where(r => r.StudentId == userId)
                .Select(r => new 
                {
                    Id = r.GroupLessonId, 
                    CoachName = r.GroupLesson.Coach.FirstName + " " + r.GroupLesson.Coach.LastName,
                    StartTime = r.GroupLesson.StartTime,
                    EndTime = r.GroupLesson.EndTime,
                    Status = "Confirmed", 
                    LessonType = "Group"
                })
                .ToListAsync();

            var allBookings = privateLessons.Concat(groupLessons)
                .OrderByDescending(x => x.StartTime)
                .ToList();

            return Ok(allBookings);
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
        // İPTAL İŞLEMİ
        // ==========================================
        [HttpPut("cancel-my-booking/{bookingId}")]
        public async Task<IActionResult> CancelMyBooking(int bookingId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // 1. Önce Bireysel Tabloya Bak
            var privateBooking = await _context.LessonBookings
                .FirstOrDefaultAsync(b => b.Id == bookingId && b.StudentId == userId);

            if (privateBooking != null)
            {
                if (privateBooking.StartTime < DateTime.UtcNow) return BadRequest("Geçmiş ders iptal edilemez.");
                
                // 🛑 YENİ KURAL: 12 SAAT KALA İPTAL EDİLEMEZ
                if (privateBooking.StartTime < DateTime.UtcNow.AddHours(12)) 
                    return BadRequest("Dersin başlamasına 12 saatten az kaldığı için iptal işlemi yapılamaz.");

                if (privateBooking.Status == BookingStatus.Cancelled) return BadRequest("Zaten iptal edilmiş.");

                using var transaction = await _context.Database.BeginTransactionAsync();
                try {
                    privateBooking.Status = BookingStatus.Cancelled;
                    var student = await _userManager.FindByIdAsync(userId);
                    student.LessonCredits += 1; // İade
                    await _userManager.UpdateAsync(student);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return Ok(new { message = "Özel ders iptal edildi." });
                } catch { await transaction.RollbackAsync(); return StatusCode(500, "Hata."); }
            }

            // 2. Yoksa Grup Tablosuna Bak (Registration -> GroupLesson)
            var groupReg = await _context.GroupLessonRegistrations
                .Include(r => r.GroupLesson)
                .FirstOrDefaultAsync(r => r.GroupLessonId == bookingId && r.StudentId == userId);

            if (groupReg != null)
            {
                if (groupReg.GroupLesson.StartTime < DateTime.UtcNow) return BadRequest("Geçmiş ders iptal edilemez.");
                
                // 🛑 YENİ KURAL: 12 SAAT KALA İPTAL EDİLEMEZ
                if (groupReg.GroupLesson.StartTime < DateTime.UtcNow.AddHours(12)) 
                    return BadRequest("Dersin başlamasına 12 saatten az kaldığı için iptal işlemi yapılamaz.");

                using var transaction = await _context.Database.BeginTransactionAsync();
                try {
                    // Kaydı sil
                    _context.GroupLessonRegistrations.Remove(groupReg);
                    
                    // Dersin kendisini de sil (Çünkü bu "özel grup" dersiydi)
                    var groupLesson = await _context.GroupLessons.FindAsync(bookingId);
                    if (groupLesson != null) _context.GroupLessons.Remove(groupLesson);

                    var student = await _userManager.FindByIdAsync(userId);
                    student.GroupCredits += 1; // İade
                    
                    await _userManager.UpdateAsync(student);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return Ok(new { message = "Grup dersi iptal edildi." });
                } catch { await transaction.RollbackAsync(); return StatusCode(500, "Hata."); }
            }

            return NotFound("Ders bulunamadı.");
        }
    }
}