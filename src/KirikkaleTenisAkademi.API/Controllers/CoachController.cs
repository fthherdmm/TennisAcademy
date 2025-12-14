using KirikkaleTenisAkademi.Domain.Entities;
using KirikkaleTenisAkademi.Domain.Enums;
using KirikkaleTenisAkademi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using KirikkaleTenisAkademi.Web.Models;

namespace KirikkaleTenisAkademi.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Coach")] // 🔒 Sadece Koçlar Girebilir
    public class CoachController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CoachController(AppDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // 1. TAKVİMİM (Bana alınan dersleri getir)
        // ==========================================
        [HttpGet("my-schedule")]
        public async Task<IActionResult> GetMySchedule()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // DÜZELTME 1: ApplicationUserId yerine AppUserId
            var coach = await _context.Coaches.FirstOrDefaultAsync(c => c.AppUserId == userId);
            if (coach == null) return NotFound("Koç profili bulunamadı.");

            var bookings = await _context.LessonBookings
                .Include(b => b.Student)
                .Where(b => b.CoachId == coach.Id && b.Status != BookingStatus.Cancelled)
                // DÜZELTME 2: Date yerine StartTime kullanıyoruz
                .OrderBy(b => b.StartTime) 
                .Select(b => new 
                {
                    BookingId = b.Id,
                    // Frontend'de tarih ve saati ayrı göstermek istersen:
                    FullDate = b.StartTime, // "2023-10-05T14:00:00"
                    DateStr = b.StartTime.ToString("dd.MM.yyyy"), // "05.10.2023"
                    Hour = b.StartTime.Hour, // 14
                    StudentName = b.Student != null ? $"{b.Student.FirstName} {b.Student.LastName}" : "İsimsiz",
                    Status = b.Status.ToString()
                })
                .ToListAsync();

            return Ok(bookings);
        }

        // ==========================================
        // 2. DERS İPTAL ET & KREDİ İADE ET (Refund)
        // ==========================================
        [HttpPost("cancel-lesson/{bookingId}")]
        public async Task<IActionResult> CancelLesson(int bookingId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            // DÜZELTME: AppUserId
            var coach = await _context.Coaches.FirstOrDefaultAsync(c => c.AppUserId == userId);
            
            if (coach == null) return Unauthorized("Koç profili bulunamadı.");

            var booking = await _context.LessonBookings
                .Include(b => b.Student) 
                .FirstOrDefaultAsync(b => b.Id == bookingId);

            if (booking == null) return NotFound("Ders bulunamadı.");

            if (booking.CoachId != coach.Id)
                return BadRequest("Size ait olmayan bir dersi iptal edemezsiniz.");

            if (booking.Status == BookingStatus.Cancelled || booking.Status == BookingStatus.Completed)
                return BadRequest("Bu ders zaten iptal edilmiş veya tamamlanmış.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // A. KREDİ İADESİ
                if (booking.Student != null)
                {
                    booking.Student.LessonCredits += 1;
                }

                // B. STATÜ GÜNCELLEME
                booking.Status = BookingStatus.Cancelled;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { message = "Ders iptal edildi ve öğrencinin kredisi iade edildi." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "İptal sırasında hata oluştu.", error = ex.Message });
            }
        }

        // ==========================================
        // 3. ÖĞRENCİLERİM (Benden ders alanlar)
        // ==========================================
        [HttpGet("my-students")]
        public async Task<IActionResult> GetMyStudents()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            // DÜZELTME: AppUserId
            var coach = await _context.Coaches.FirstOrDefaultAsync(c => c.AppUserId == userId);
            
            if (coach == null) return NotFound("Koç profili bulunamadı.");

            var students = await _context.LessonBookings
                .Where(b => b.CoachId == coach.Id && b.Student != null)
                .Select(b => b.Student)
                .Distinct()
                .Select(s => new 
                {
                    s.Id,
                    FullName = $"{s.FirstName} {s.LastName}",
                    s.Email,
                    s.ProfileImageUrl
                })
                .ToListAsync();

            return Ok(students);
        }
        
        // ==========================================
        // 4. MÜSAİTLİK YÖNETİMİ (Zaman Kapatma)
        // ==========================================

        public class BlockTimeRequest
        {
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public string? Reason { get; set; }
        }

        [HttpPost("block-time")]
        public async Task<IActionResult> BlockTime([FromBody] BlockTimeRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var coach = await _context.Coaches.FirstOrDefaultAsync(c => c.AppUserId == userId);
            if (coach == null) return Unauthorized("Koç profili bulunamadı.");

            // Validasyon 1: Geçmişe blok konamaz
            if (request.StartTime < DateTime.Now)
                return BadRequest("Geçmiş bir tarihi kapatamazsınız.");

            // Validasyon 2: Bitiş, Başlangıçtan sonra olmalı
            if (request.EndTime <= request.StartTime)
                return BadRequest("Bitiş saati başlangıçtan ileri olmalı.");

            // Validasyon 3: O saatte zaten dersi var mı? (Çok Kritik!)
            // Koçun o saat aralığında ÇAKIŞAN (Overlap) bir dersi varsa izin verme.
            var hasLesson = await _context.LessonBookings
                .AnyAsync(b => b.CoachId == coach.Id 
                               && b.Status == BookingStatus.Confirmed
                               && b.StartTime < request.EndTime 
                               && b.EndTime > request.StartTime);

            if (hasLesson)
                return BadRequest("Bu saat aralığında zaten onaylanmış bir dersiniz var. Önce dersi iptal etmelisiniz.");

            // Kaydet
            var unavailable = new CoachUnavailability
            {
                CoachId = coach.Id,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                Reason = request.Reason
            };

            _context.CoachUnavailabilities.Add(unavailable);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Seçilen zaman aralığı başarıyla kapatıldı." });
        }

        [HttpGet("my-unavailabilities")]
        public async Task<IActionResult> GetMyUnavailabilities()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var coach = await _context.Coaches.FirstOrDefaultAsync(c => c.AppUserId == userId);
            if (coach == null) return Unauthorized();

            var blocks = await _context.CoachUnavailabilities
                .Where(u => u.CoachId == coach.Id && u.EndTime > DateTime.Now) // Sadece gelecektekiler
                .OrderBy(u => u.StartTime)
                .Select(u => new 
                {
                    u.Id,
                    u.StartTime,
                    u.EndTime,
                    u.Reason
                })
                .ToListAsync();

            return Ok(blocks);
        }

        [HttpDelete("unblock-time/{id}")]
        public async Task<IActionResult> UnblockTime(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var coach = await _context.Coaches.FirstOrDefaultAsync(c => c.AppUserId == userId);
            if (coach == null) return Unauthorized();

            var block = await _context.CoachUnavailabilities.FindAsync(id);
            if (block == null) return NotFound("Kayıt bulunamadı.");

            if (block.CoachId != coach.Id) return BadRequest("Bu işlem size ait değil.");

            _context.CoachUnavailabilities.Remove(block);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Zaman kilidi kaldırıldı, artık ders alabilirsiniz." });
        }
        
        [HttpGet("{coachId}/public-schedule")]
        [AllowAnonymous] // Öğrenci giriş yapmış olsa da olmasa da veriyi çekebilsin
        public async Task<IActionResult> GetCoachPublicSchedule(int coachId)
        {
            // 1. Hocanın Dersleri (Dolu)
            var lessons = await _context.LessonBookings
                .Where(b => b.CoachId == coachId && 
                            (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Pending))
                .Select(b => new PublicCoachScheduleDto // DTO ile dönüyoruz
                {
                    StartTime = b.StartTime,
                    EndTime = b.EndTime,
                    Type = "Lesson" 
                })
                .ToListAsync();

            // 2. Hocanın Kapattığı Saatler
            var blocks = await _context.CoachUnavailabilities
                .Where(u => u.CoachId == coachId)
                .Select(u => new PublicCoachScheduleDto // DTO ile dönüyoruz
                {
                    StartTime = u.StartTime,
                    EndTime = u.EndTime,
                    Type = "Blocked" 
                })
                .ToListAsync();

            // 3. Birleştir
            var fullSchedule = new List<PublicCoachScheduleDto>();
            fullSchedule.AddRange(lessons);
            fullSchedule.AddRange(blocks);

            return Ok(fullSchedule);
        }
    }
}