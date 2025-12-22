using System.Security.Claims;
using KirikkaleTenisAkademi.Domain.Entities;
using KirikkaleTenisAkademi.Domain.Enums;
using KirikkaleTenisAkademi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KirikkaleTenisAkademi.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BookingsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public BookingsController(AppDbContext context)
        {
            _context = context;
        }

        public class BookingRequest
        {
            public int CoachId { get; set; }
            public DateTime StartTime { get; set; } // UTC Olarak gelecek
        }

        // 1. DERS REZERVASYONU YAP
        [HttpPost("book")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> BookLesson([FromBody] BookingRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _context.Users.FindAsync(userId);

            // A. Kredi Kontrolü
            if (user.LessonCredits < 1)
                return BadRequest("Ders almak için yeterli krediniz yok. Lütfen paket satın alın.");

            // Ders süresi standart 1 saat olsun (İstersen parametre yapabiliriz)
            var endTime = request.StartTime.AddHours(1);

            // B. Koç Müsaitlik Kontrolü (CoachUnavailability tablosuna bak)
            var isCoachUnavailable = await _context.CoachUnavailabilities
                .AnyAsync(u => u.CoachId == request.CoachId &&
                               (u.StartTime < endTime && u.EndTime > request.StartTime)); // Çakışma kontrolü

            if (isCoachUnavailable)
                return BadRequest("Koç bu saatte müsait değil.");

            // C. Başka Ders Var mı? (Confirmed olan derslerle çakışıyor mu?)
            var existingBooking = await _context.LessonBookings
                .AnyAsync(b => b.CoachId == request.CoachId &&
                               b.Status == BookingStatus.Confirmed &&
                               (b.StartTime < endTime && b.EndTime > request.StartTime));

            if (existingBooking)
                return BadRequest("Bu saatte koçun başka bir dersi var.");

            // D. Rezervasyonu Oluştur
            var booking = new LessonBooking
            {
                StudentId = userId,
                CoachId = request.CoachId,
                StartTime = request.StartTime,
                EndTime = endTime,
                Status = BookingStatus.Pending, // Onay bekliyor
                CreatedDate = DateTime.UtcNow
            };

            // E. Krediyi Düş ve Kaydet
            user.LessonCredits -= 1; // 1 Kredi düştük
            _context.LessonBookings.Add(booking);
            
            await _context.SaveChangesAsync();

            return Ok(new { message = "Ders talebiniz alındı. Koç onayı bekleniyor.", remainingCredits = user.LessonCredits });
        }

        // 2. KOÇ ONAY/RED İŞLEMİ
        public class ApproveRequest { public bool IsApproved { get; set; } }

        [HttpPost("approve/{bookingId}")]
        [Authorize(Roles = "Coach")]
        public async Task<IActionResult> ApproveBooking(int bookingId, [FromBody] ApproveRequest request)
        {
            var booking = await _context.LessonBookings.Include(b => b.Student).FirstOrDefaultAsync(b => b.Id == bookingId);
            if (booking == null) return NotFound();

            if (request.IsApproved)
            {
                booking.Status = BookingStatus.Confirmed;
            }
            else
            {
                booking.Status = BookingStatus.Rejected;
                // Reddedildiyse Krediyi İADE ET
                if (booking.Student != null)
                {
                    booking.Student.LessonCredits += 1;
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = request.IsApproved ? "Ders onaylandı." : "Ders reddedildi ve kredi iade edildi." });
        }
        
        // 3. ÖĞRENCİNİN DERSLERİNİ GETİR
        [HttpGet("my-bookings")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> GetMyBookings()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var bookings = await _context.LessonBookings
                .Include(b => b.Coach)
                .Where(b => b.StudentId == userId)
                .OrderByDescending(b => b.StartTime)
                .Select(b => new
                {
                    b.Id,
                    b.StartTime,
                    b.EndTime,
                    Status = (int)b.Status,
                    CoachName = b.Coach.FirstName + " " + b.Coach.LastName
                })
                .ToListAsync();

            return Ok(bookings);
        }
        
        // ... Diğer metodların altına ekleyin ...

        // 4. DROPDOWN İÇİN KOÇ LİSTESİ (Hafif Liste)
        [HttpGet("coaches-list")]
        [Authorize] // Üye olan herkes görebilir
        public async Task<IActionResult> GetCoachesForList()
        {
            var coaches = await _context.Coaches
                .Select(c => new 
                {
                    c.Id,
                    FullName = c.FirstName + " " + c.LastName
                })
                .ToListAsync();

            return Ok(coaches);
        }
    }
}