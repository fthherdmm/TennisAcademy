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
            // 1. KULLANICIYI BUL
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var student = await _userManager.FindByIdAsync(userId);

            if (student == null) return Unauthorized("Kullanıcı bulunamadı.");

            // 2. KREDİ KONTROLÜ
            if (student.LessonCredits < 1)
            {
                return BadRequest("Yetersiz bakiye! Lütfen önce paket satın alınız.");
            }

            // 3. MÜSAİTLİK KONTROLÜ (Çok Önemli!)
            // A. O saatte başka bir DERS var mı?
            var isLessonExists = await _context.LessonBookings
                .AnyAsync(b => b.CoachId == request.CoachId 
                            && b.Status != BookingStatus.Cancelled
                            && b.StartTime < request.EndTime 
                            && b.EndTime > request.StartTime);

            if (isLessonExists) return BadRequest("Bu saatte zaten bir rezervasyon var.");

            // B. Hoca o saati KAPATMIŞ mı? (CoachUnavailability)
            var isBlocked = await _context.CoachUnavailabilities
                .AnyAsync(u => u.CoachId == request.CoachId
                            && u.StartTime < request.EndTime
                            && u.EndTime > request.StartTime);

            if (isBlocked) return BadRequest("Eğitmen bu saat aralığında müsait değil.");

            // 4. İŞLEMİ YAP (Transaction ile güvenli mod)
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Krediyi Düş
                student.LessonCredits -= 1;
                await _userManager.UpdateAsync(student); // Kredi güncellemesini kaydet

                // Rezervasyonu Oluştur
                var booking = new LessonBooking
                {
                    StudentId = student.Id,
                    CoachId = request.CoachId,
                    StartTime = request.StartTime.ToUniversalTime(), 
                    EndTime = request.EndTime.ToUniversalTime(),
                    Status = BookingStatus.Confirmed, // Kredili olduğu için direkt onaylı
                    CreatedDate = DateTime.UtcNow
                };

                _context.LessonBookings.Add(booking);
                await _context.SaveChangesAsync();

                // İşlemi onayla
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
    }
}