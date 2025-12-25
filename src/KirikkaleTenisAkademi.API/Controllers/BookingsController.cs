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

        [HttpPost("book")]
[Authorize(Roles = "Student")]
public async Task<IActionResult> BookLesson([FromBody] BookingRequest request)
{
    // ==============================================================================
    // 1. SAAT DÜZELTMESİ (Timezone Fix)
    // ==============================================================================
    
    // Frontend'den gelen saati (Örn: 14:00) Türkiye Saati olarak kabul ediyoruz.
    // Bunu veritabanına UTC (Örn: 11:00) olarak kaydetmeliyiz.
    
    DateTime startUtc;
    DateTime endUtc;

    try 
    {
        // Sunucu Windows ise "Turkey Standard Time", Linux/Docker ise "Europe/Istanbul"
        string timeZoneId = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows) 
            ? "Turkey Standard Time" 
            : "Europe/Istanbul";

        TimeZoneInfo trTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

        // Gelen tarihi TR saati kabul et -> UTC'ye çevir
        startUtc = TimeZoneInfo.ConvertTimeToUtc(request.StartTime, trTimeZone);
        
        // Bitiş saati = Başlangıç + 1 Saat
        endUtc = startUtc.AddHours(1);
    }
    catch
    {
        // Eğer sunucuda timezone bulunamazsa manuel -3 saat yap (Yedek plan)
        startUtc = request.StartTime.AddHours(-3);
        endUtc = startUtc.AddHours(1);
    }

    // ==============================================================================
    // 2. KULLANICI VE KREDİ KONTROLÜ
    // ==============================================================================

    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
    var user = await _context.Users.FindAsync(userId);

    if (user == null) return Unauthorized("Kullanıcı bulunamadı.");

    if (user.LessonCredits < 1)
        return BadRequest("Ders almak için yeterli krediniz yok. Lütfen paket satın alın.");

    // ==============================================================================
    // 3. MÜSAİTLİK KONTROLLERİ (UTC SAATLERLE)
    // ==============================================================================

    // A. Koç o saatte "Müsait Değil" olarak işaretlemiş mi? (Blocked)
    var isCoachBlocked = await _context.CoachUnavailabilities
        .AnyAsync(u => u.CoachId == request.CoachId &&
                       (u.StartTime < endUtc && u.EndTime > startUtc)); 

    if (isCoachBlocked)
        return BadRequest("Koç bu saatte müsait değil (Kapalı).");

    // B. Koçun o saatte BAŞKA bir dersi var mı?
    var isCoachBusy = await _context.LessonBookings
        .AnyAsync(b => b.CoachId == request.CoachId &&
                       b.Status != BookingStatus.Cancelled && 
                       b.Status != BookingStatus.Rejected &&
                       (b.StartTime < endUtc && b.EndTime > startUtc));

    if (isCoachBusy)
        return BadRequest("Bu saatte koçun başka bir dersi var.");

    // C. (YENİ) ÖĞRENCİNİN o saatte başka dersi var mı? (Kendini kopyalayamaz)
    var isStudentBusy = await _context.LessonBookings
        .AnyAsync(b => b.StudentId == userId &&
                       b.Status != BookingStatus.Cancelled &&
                       b.Status != BookingStatus.Rejected &&
                       (b.StartTime < endUtc && b.EndTime > startUtc));

    if (isStudentBusy)
        return BadRequest("Bu saat aralığında zaten başka bir dersiniz var.");

    // ==============================================================================
    // 4. REZERVASYON OLUŞTURMA (Transaction ile Güvenli Kayıt)
    // ==============================================================================
    
    using var transaction = await _context.Database.BeginTransactionAsync();
    try
    {
        // Krediyi düş
        user.LessonCredits -= 1;
        _context.Users.Update(user); // User tablosunu güncelle

        // Rezervasyonu oluştur
        var booking = new LessonBooking
        {
            StudentId = userId,
            CoachId = request.CoachId,
            StartTime = startUtc, // Hesapladığımız doğru UTC zamanı
            EndTime = endUtc,     // Hesapladığımız doğru UTC zamanı
            Status = BookingStatus.Confirmed, // Kredili olduğu için direkt ONAYLI
            CreatedDate = DateTime.UtcNow
        };

        _context.LessonBookings.Add(booking);
        
        // Hepsini kaydet ve işlemi tamamla
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return Ok(new { message = "Rezervasyonunuz başarıyla oluşturuldu.", remainingCredits = user.LessonCredits });
    }
    catch (Exception ex)
    {
        // Hata olursa işlemi geri al (Kredi yanmasın)
        await transaction.RollbackAsync();
        return StatusCode(500, "İşlem sırasında bir hata oluştu: " + ex.Message);
    }
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