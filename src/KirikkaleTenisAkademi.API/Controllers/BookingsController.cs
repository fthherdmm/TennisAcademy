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
            public DateTime StartTime { get; set; } // UTC Olarak gelmesi bekleniyor ama biz yine de garantiliyoruz
            public string LessonType { get; set; } = "Private"; // Varsayılan: Private. "Group" gelebilir.
        }

        [HttpPost("book")]
        [Authorize(Roles = "Student")]
[Authorize(Roles = "Student")]
public async Task<IActionResult> BookLesson([FromBody] BookingRequest request)
{
    // ==============================================================================
    // 1. SAAT DÜZELTMESİ (Timezone Fix - ORTAK)
    // ==============================================================================
    DateTime startUtc;
    DateTime endUtc;

    try 
    {
        string timeZoneId = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows) 
            ? "Turkey Standard Time" 
            : "Europe/Istanbul";

        TimeZoneInfo trTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

        // Gelen tarihi TR saati kabul et -> UTC'ye çevir
        startUtc = TimeZoneInfo.ConvertTimeToUtc(request.StartTime, trTimeZone);
        
        // Bitiş saati = Başlangıç + 1 Saat (Grup derslerinde süre farklı olabilir ama başlangıç saati esastır)
        endUtc = startUtc.AddHours(1);
    }
    catch
    {
        startUtc = request.StartTime.AddHours(-3);
        endUtc = startUtc.AddHours(1);
    }

    // Kullanıcıyı Bul
    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
    var user = await _context.Users.FindAsync(userId);
    if (user == null) return Unauthorized("Kullanıcı bulunamadı.");
    
    // ==============================================================================
    // A. GRUP DERSİ SENARYOSU
    // ==============================================================================
    if (request.LessonType == "Group")
    {
        // 1. Kredi Kontrolü (Grup Kredisi)
        if (user.GroupCredits < 1)
            return BadRequest("Grup dersine katılmak için yeterli 'Grup Krediniz' yok.");

        // 2. Ders Kontrolü: O saatte koçun açtığı bir grup dersi var mı?
        // Not: StartTime dakika hassasiyeti önemlidir.
        var groupLesson = await _context.GroupLessons
            .Include(g => g.Registrations)
            .FirstOrDefaultAsync(g => g.CoachId == request.CoachId 
                                   && g.StartTime == startUtc 
                                   && g.IsActive);

        if (groupLesson == null)
            return BadRequest("Seçilen saatte bu eğitmenin planlanmış bir grup dersi bulunmuyor.");

        // 3. Kapasite Kontrolü
        if (groupLesson.Registrations.Count >= groupLesson.Capacity)
            return BadRequest("Bu grup dersinin kontenjanı dolmuş.");

        // 4. Mükerrer Kayıt Kontrolü (Zaten kayıtlı mı?)
        if (groupLesson.Registrations.Any(r => r.StudentId == userId))
            return BadRequest("Bu derse zaten kayıtlısınız.");

        // 5. İŞLEM (Transaction)
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Grup Kredisini Düş
            user.GroupCredits -= groupLesson.CreditCost; // Genelde 1'dir ama dinamik alalım
            _context.Users.Update(user);

            // Kaydı Oluştur
            var registration = new GroupLessonRegistration
            {
                GroupLessonId = groupLesson.Id,
                StudentId = userId,
                RegistrationDate = DateTime.UtcNow
            };
            _context.GroupLessonRegistrations.Add(registration);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new { message = "Grup dersine kaydınız başarıyla yapıldı.", remainingCredits = user.GroupCredits });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, "Grup kaydı sırasında hata: " + ex.Message);
        }
    }
    // ==============================================================================
    // B. ÖZEL DERS SENARYOSU (Mevcut Kod)
    // ==============================================================================
    else 
    {
        // 1. Kredi Kontrolü (Özel Ders Kredisi)
        if (user.LessonCredits < 1)
            return BadRequest("Özel ders almak için yeterli krediniz yok.");

        // 2. Müsaitlik Kontrolleri
        
        // Koç engelli mi?
        var isCoachBlocked = await _context.CoachUnavailabilities
            .AnyAsync(u => u.CoachId == request.CoachId &&
                        (u.StartTime < endUtc && u.EndTime > startUtc)); 

        if (isCoachBlocked) return BadRequest("Koç bu saatte müsait değil (Kapalı).");

        // Koçun başka dersi var mı? (Özel Ders)
        var isCoachBusyPrivate = await _context.LessonBookings
            .AnyAsync(b => b.CoachId == request.CoachId &&
                        b.Status != BookingStatus.Cancelled && 
                        b.Status != BookingStatus.Rejected &&
                        (b.StartTime < endUtc && b.EndTime > startUtc));

        // Koçun o saatte Grup Dersi var mı? (Çakışma Kontrolü)
        var isCoachBusyGroup = await _context.GroupLessons
            .AnyAsync(g => g.CoachId == request.CoachId &&
                           g.IsActive &&
                           (g.StartTime < endUtc && g.EndTime > startUtc));

        if (isCoachBusyPrivate || isCoachBusyGroup)
            return BadRequest("Bu saatte koçun başka bir dersi (Özel veya Grup) var.");

        // Öğrenci meşgul mü?
        var isStudentBusy = await _context.LessonBookings
            .AnyAsync(b => b.StudentId == userId &&
                        b.Status != BookingStatus.Cancelled &&
                        b.Status != BookingStatus.Rejected &&
                        (b.StartTime < endUtc && b.EndTime > startUtc));

        if (isStudentBusy)
            return BadRequest("Bu saat aralığında zaten başka bir dersiniz var.");

        // 3. İŞLEM (Transaction)
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Özel Ders Kredisini Düş
            user.LessonCredits -= 1;
            _context.Users.Update(user);

            var booking = new LessonBooking
            {
                StudentId = userId,
                CoachId = request.CoachId,
                StartTime = startUtc,
                EndTime = endUtc,
                Status = BookingStatus.Confirmed,
                CreatedDate = DateTime.UtcNow
            };

            _context.LessonBookings.Add(booking);
            
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new { message = "Özel ders rezervasyonunuz oluşturuldu.", remainingCredits = user.LessonCredits });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, "Özel ders kaydı sırasında hata: " + ex.Message);
        }
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