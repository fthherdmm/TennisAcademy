using KirikkaleTenisAkademi.Domain.Entities;
using KirikkaleTenisAkademi.Domain.Enums;
using KirikkaleTenisAkademi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using KirikkaleTenisAkademi.Application.DTOs;
using KirikkaleTenisAkademi.Web.Models;
using Microsoft.AspNetCore.Identity;

namespace KirikkaleTenisAkademi.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Coach")] // 🔒 Sadece Koçlar Girebilir
    public class CoachController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public CoachController(AppDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ==========================================
        // 1. TAKVİMİM (Liste Görünümü)
        // ==========================================
        [HttpGet("my-schedule")]
        public async Task<IActionResult> GetMySchedule()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var coach = await _context.Coaches.FirstOrDefaultAsync(c => c.AppUserId == userId);
            if (coach == null) return NotFound("Koç profili bulunamadı.");

            // Hem Bireysel Hem Grup Derslerini Getirelim
            var privateBookings = await _context.LessonBookings
                .Include(b => b.Student)
                .Where(b => b.CoachId == coach.Id && b.Status != BookingStatus.Cancelled)
                .Select(b => new 
                {
                    BookingId = b.Id,
                    FullDate = b.StartTime,
                    DateStr = b.StartTime.ToString("dd.MM.yyyy"),
                    Hour = b.StartTime.Hour,
                    StudentName = b.Student != null ? $"{b.Student.FirstName} {b.Student.LastName}" : "İsimsiz",
                    Status = "Private"
                })
                .ToListAsync();

            var groupLessons = await _context.GroupLessons
                .Where(g => g.CoachId == coach.Id && g.IsActive)
                .Select(g => new 
                {
                    BookingId = g.Id,
                    FullDate = g.StartTime,
                    DateStr = g.StartTime.ToString("dd.MM.yyyy"),
                    Hour = g.StartTime.Hour,
                    StudentName = g.Title, // "Ahmet (Grup)" yazar
                    Status = "Group"
                })
                .ToListAsync();

            var all = privateBookings.Concat(groupLessons).OrderBy(x => x.FullDate).ToList();

            return Ok(all);
        }

        // ==========================================
        // 2. ÖĞRENCİNİN YAKLAŞAN DERSLERİ
        // ==========================================
        // CoachController.cs içine yapıştır (Mevcut olanı değiştir)

        [HttpGet("student-lessons/{studentId}")]
        public async Task<IActionResult> GetStudentLessons(string studentId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var coach = await _context.Coaches.FirstOrDefaultAsync(c => c.AppUserId == userId);
            if (coach == null) return Unauthorized("Koç bulunamadı.");

            // 1. ÖZEL DERSLER (Koçun bu öğrenciyle olan dersleri)
            var privateLessons = await _context.LessonBookings
                .Where(b => b.CoachId == coach.Id 
                            && b.StudentId == studentId
                            && b.Status != BookingStatus.Cancelled
                            && b.StartTime >= DateTime.UtcNow) 
                .Select(b => new 
                {
                    Id = b.Id,
                    StartTime = b.StartTime,
                    EndTime = b.EndTime,
                    Type = "Private", // Ayrım için
                    Title = "Özel Ders"
                })
                .ToListAsync();

            // 2. GRUP DERSLERİ (Öğrencinin kayıtlı olduğu ve BU KOÇUN verdiği dersler)
            var groupLessons = await _context.GroupLessonRegistrations
                .Include(r => r.GroupLesson)
                .Where(r => r.StudentId == studentId 
                            && r.GroupLesson.CoachId == coach.Id // Sadece bu koçun dersleri
                            && r.GroupLesson.StartTime >= DateTime.UtcNow)
                .Select(r => new 
                {
                    Id = r.GroupLessonId, // Grup dersi ID'si
                    StartTime = r.GroupLesson.StartTime,
                    EndTime = r.GroupLesson.EndTime,
                    Type = "Group",
                    Title = r.GroupLesson.Title ?? "Grup Dersi"
                })
                .ToListAsync();

            // 3. LİSTELERİ BİRLEŞTİR VE SIRALA
            var allLessons = privateLessons.Concat(groupLessons)
                                           .OrderBy(x => x.StartTime)
                                           .ToList();

            return Ok(allLessons);
        }
        
        // ==========================================
        // 3. DERS İPTAL ET (Sadece Bireysel)
        // ==========================================
        [HttpPost("cancel-lesson/{bookingId}")]
        public async Task<IActionResult> CancelLesson(int bookingId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var coach = await _context.Coaches.FirstOrDefaultAsync(c => c.AppUserId == userId);
            if (coach == null) return Unauthorized("Koç profili bulunamadı.");

            var booking = await _context.LessonBookings
                .Include(b => b.Student) 
                .FirstOrDefaultAsync(b => b.Id == bookingId);

            if (booking == null) return NotFound("Ders bulunamadı.");

            if (booking.CoachId != coach.Id)
                return BadRequest("Size ait olmayan bir dersi iptal edemezsiniz.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (booking.Student != null) booking.Student.LessonCredits += 1;
                booking.Status = BookingStatus.Cancelled;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { message = "Ders iptal edildi ve iade yapıldı." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Hata oluştu.", error = ex.Message });
            }
        }
        
        // CoachController.cs içine ekle:

        [HttpPost("cancel-group-lesson/{groupLessonId}")]
        public async Task<IActionResult> CancelGroupLesson(int groupLessonId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var coach = await _context.Coaches.FirstOrDefaultAsync(c => c.AppUserId == userId);
            if (coach == null) return Unauthorized();

            // 1. Grup Dersini ve Kayıtlı Öğrenciyi Bul
            var groupLesson = await _context.GroupLessons
                .Include(g => g.Registrations) // Kayıtlı öğrenciyi bulmak için
                .FirstOrDefaultAsync(g => g.Id == groupLessonId);

            if (groupLesson == null) return NotFound("Grup dersi bulunamadı.");

            // 2. Güvenlik: Bu ders bu hocanın mı?
            if (groupLesson.CoachId != coach.Id)
                return BadRequest("Size ait olmayan bir dersi iptal edemezsiniz.");

            // 3. İŞLEM (Dersi Sil ve İade Et)
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Eğer derse kayıtlı bir öğrenci varsa (ki olmak zorunda), kredisini iade et
                var registration = groupLesson.Registrations.FirstOrDefault();
                if (registration != null)
                {
                    var student = await _userManager.FindByIdAsync(registration.StudentId);
                    if (student != null)
                    {
                        student.GroupCredits += groupLesson.CreditCost; // Kredi İade
                        await _userManager.UpdateAsync(student);
                    }
                }

                // Grup Dersini Komple Sil (Cascade ile registration da silinir)
                _context.GroupLessons.Remove(groupLesson);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { message = "Grup dersi iptal edildi ve kredi iade edildi." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, "İşlem sırasında hata: " + ex.Message);
            }
        }

        // ==========================================
        // 4. ÖĞRENCİLERİM
        // ==========================================
        [HttpGet("my-students")]
        public async Task<IActionResult> GetMyStudents()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
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
        // 5. MÜSAİTLİK YÖNETİMİ (Zaman Kapatma)
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

            DateTime startUtc, endUtc;
            try 
            {
                string timeZoneId = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows) 
                    ? "Turkey Standard Time" : "Europe/Istanbul";
                TimeZoneInfo trTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

                startUtc = TimeZoneInfo.ConvertTimeToUtc(request.StartTime, trTimeZone);
                endUtc = TimeZoneInfo.ConvertTimeToUtc(request.EndTime, trTimeZone);
            }
            catch
            {
                startUtc = request.StartTime.AddHours(-3);
                endUtc = request.EndTime.AddHours(-3);
            }

            if (startUtc < DateTime.UtcNow) return BadRequest("Geçmiş bir tarihi kapatamazsınız.");
            if (endUtc <= startUtc) return BadRequest("Bitiş saati hatalı.");

            // Kontrol: O saatte Bireysel Ders var mı?
            var hasPrivate = await _context.LessonBookings.AnyAsync(b => b.CoachId == coach.Id && b.Status == BookingStatus.Confirmed && b.StartTime < endUtc && b.EndTime > startUtc);
            
            // Kontrol: O saatte Grup Dersi var mı?
            var hasGroup = await _context.GroupLessons.AnyAsync(g => g.CoachId == coach.Id && g.IsActive && g.StartTime < endUtc && g.EndTime > startUtc);

            if (hasPrivate || hasGroup) return BadRequest("Bu saat aralığında dersiniz var, kapatamazsınız.");

            var unavailable = new CoachUnavailability
            {
                CoachId = coach.Id,
                StartTime = startUtc,
                EndTime = endUtc,
                Reason = request.Reason
            };

            _context.CoachUnavailabilities.Add(unavailable);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Zaman aralığı kapatıldı." });
        }

        [HttpGet("my-unavailabilities")]
        public async Task<IActionResult> GetMyUnavailabilities()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var coach = await _context.Coaches.FirstOrDefaultAsync(c => c.AppUserId == userId);
            if (coach == null) return Unauthorized();

            var blocks = await _context.CoachUnavailabilities
                .Where(u => u.CoachId == coach.Id && u.EndTime > DateTime.UtcNow)
                .OrderBy(u => u.StartTime)
                .Select(u => new { u.Id, u.StartTime, u.EndTime, u.Reason })
                .ToListAsync();

            return Ok(blocks);
        }

        [HttpDelete("unblock-time/{id}")]
        public async Task<IActionResult> UnblockTime(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var coach = await _context.Coaches.FirstOrDefaultAsync(c => c.AppUserId == userId);
            
            var block = await _context.CoachUnavailabilities.FindAsync(id);
            if (block == null) return NotFound();
            if (block.CoachId != coach.Id) return BadRequest();

            _context.CoachUnavailabilities.Remove(block);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Zaman kilidi kaldırıldı." });
        }
        
        // ==========================================
        // 6. ÖĞRENCİ TARAFINDAN GÖRÜLEN TAKVİM (ÖNEMLİ)
        // ==========================================
        [HttpGet("{coachId}/public-schedule")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCoachPublicSchedule(int coachId)
        {
            var fullSchedule = new List<PublicCoachScheduleDto>();

            // A. Özel Dersler -> "PrivateLesson"
            var privateLessons = await _context.LessonBookings
                .Where(b => b.CoachId == coachId && (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Pending))
                .Select(b => new PublicCoachScheduleDto 
                {
                    StartTime = b.StartTime, EndTime = b.EndTime, Type = "PrivateLesson" 
                }).ToListAsync();
            fullSchedule.AddRange(privateLessons);

            // B. Grup Dersleri -> "GroupLesson" (BU EKSİKTİ, EKLENDİ)
            var groupLessons = await _context.GroupLessons
                .Where(g => g.CoachId == coachId && g.IsActive)
                .Select(g => new PublicCoachScheduleDto 
                {
                    StartTime = g.StartTime, EndTime = g.EndTime, Type = "GroupLesson" 
                }).ToListAsync();
            fullSchedule.AddRange(groupLessons);

            // C. Kapalı Saatler -> "Blocked"
            var blocks = await _context.CoachUnavailabilities
                .Where(u => u.CoachId == coachId)
                .Select(u => new PublicCoachScheduleDto 
                {
                    StartTime = u.StartTime, EndTime = u.EndTime, Type = "Blocked" 
                }).ToListAsync();
            fullSchedule.AddRange(blocks);

            return Ok(fullSchedule);
        }
        
        // ==========================================
        // 7. KOÇUN KENDİ TAM TAKVİMİ (Dersler + Bloklar)
        // ==========================================
        [HttpGet("my-full-schedule")]
        public async Task<IActionResult> GetMyFullSchedule()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var coach = await _context.Coaches.FirstOrDefaultAsync(c => c.AppUserId == userId);
            if (coach == null) return Unauthorized();

            // A. Özel Dersler
            var privateLessons = await _context.LessonBookings
                .Include(b => b.Student)
                .Where(b => b.CoachId == coach.Id && b.Status != BookingStatus.Cancelled)
                .Select(b => new 
                {
                    Id = b.Id, StartTime = b.StartTime, EndTime = b.EndTime, 
                    Type = "Lesson", // Koç takviminde yeşil
                    Title = b.Student.FirstName + " " + b.Student.LastName
                }).ToListAsync();

            // B. Grup Dersleri (BUNU DA GÖRMELİ)
            var groupLessons = await _context.GroupLessons
                .Where(g => g.CoachId == coach.Id && g.IsActive)
                .Select(g => new 
                {
                    Id = g.Id, StartTime = g.StartTime, EndTime = g.EndTime, 
                    Type = "GroupLesson", // Koç takviminde mavi (CSS'i ayarlamıştık)
                    Title = g.Title // "Ahmet (Grup)"
                }).ToListAsync();

            // C. Bloklar
            var blocks = await _context.CoachUnavailabilities
                .Where(u => u.CoachId == coach.Id)
                .Select(u => new 
                {
                    Id = u.Id, StartTime = u.StartTime, EndTime = u.EndTime, 
                    Type = "Block", 
                    Title = u.Reason ?? "Kapalı"
                }).ToListAsync();

            var fullSchedule = new List<object>();
            fullSchedule.AddRange(privateLessons);
            fullSchedule.AddRange(groupLessons);
            fullSchedule.AddRange(blocks);

            return Ok(fullSchedule);
        }
        
        // ==========================================
        // KREDİ İADESİ VE PORTFOLYO
        // ==========================================
        [HttpPost("grant-credit")]
        [Authorize(Roles = "Coach")]
        public async Task<IActionResult> GrantCredit([FromBody] GrantCreditRequest request)
        {
            var student = await _userManager.FindByIdAsync(request.StudentId);
            if (student == null) return NotFound("Öğrenci bulunamadı.");

            student.LessonCredits += request.Amount;
            await _userManager.UpdateAsync(student);
            return Ok(new { message = "Kredi yüklendi." });
        }
        
        [HttpGet("my-students-portfolio")]
        public async Task<IActionResult> GetMyStudentsPortfolio()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var coach = await _context.Coaches.FirstOrDefaultAsync(c => c.AppUserId == userId);
            if (coach == null) return Unauthorized();

            var students = await (from user in _context.Users
                    join userRole in _context.UserRoles on user.Id equals userRole.UserId
                    join role in _context.Roles on userRole.RoleId equals role.Id
                    where role.Name == "Student"
                    select new MyStudentDto
                    {
                        StudentId = user.Id,
                        FullName = user.FirstName + " " + user.LastName,
                        Email = user.Email,
                        ProfileImageUrl = user.ProfileImageUrl,
                        CurrentCredits = user.LessonCredits,
                        TCKN = user.TCKN,
                        Phone = user.PhoneNumber,
                        BirthDate = user.BirthDate,
                        Height = user.Height,
                        Weight = user.Weight,
                        Level = user.Level.ToString(), 
                        Hand = user.DominantHand.ToString(),
                        EmergencyContact = user.EmergencyContactName + " (" + user.EmergencyContactPhone + ")"
                    })
                .ToListAsync();

            return Ok(students);
        }
        
        // ==========================================
        // TURNUVA VE MAÇ YÖNETİMİ
        // ==========================================
        [HttpGet("tournaments")]
        public async Task<IActionResult> GetActiveTournaments()
        {
            return Ok(await _context.Tournaments.Where(t => t.IsActive).Select(t => new { t.Id, t.Name }).ToListAsync());
        }

        [HttpPost("add-match")]
        public async Task<IActionResult> AddMatch([FromBody] MatchDto request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var coach = await _context.Coaches.FirstOrDefaultAsync(c => c.AppUserId == userId);

            var match = new Match
            {
                TournamentId = request.TournamentId,
                MatchDate = request.MatchDate,
                Player1Id = request.StudentId,
                Player2ExternalName = request.OpponentName,
                ScoreSet1 = request.ScoreSet1,
                ScoreSet2 = request.ScoreSet2,
                ScoreSet3 = request.ScoreSet3,
                RefereeCoachId = coach.Id,
                CoachNotes = request.CoachNotes,
                WinnerId = request.IsWinner ? request.StudentId : null 
            };

            _context.Matches.Add(match);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Maç eklendi." });
        }

        [HttpGet("student-matches/{studentId}")]
        public async Task<IActionResult> GetStudentMatches(string studentId)
        {
            var matches = await _context.Matches
                .Include(m => m.Tournament)
                .Where(m => m.Player1Id == studentId || m.Player2Id == studentId)
                .OrderByDescending(m => m.MatchDate)
                .Select(m => new MatchDto
                {
                    Id = m.Id,
                    TournamentName = m.Tournament != null ? m.Tournament.Name : "Bilinmiyor",
                    MatchDate = m.MatchDate,
                    OpponentName = m.Player1Id == studentId ? m.Player2ExternalName : "Rakip", 
                    ScoreSet1 = m.ScoreSet1, ScoreSet2 = m.ScoreSet2, ScoreSet3 = m.ScoreSet3,
                    CoachNotes = m.CoachNotes,
                    IsWinner = m.WinnerId == studentId
                }).ToListAsync();
            return Ok(matches);
        }

        [HttpDelete("delete-match/{id}")]
        public async Task<IActionResult> DeleteMatch(int id)
        {
            var match = await _context.Matches.FindAsync(id);
            if (match != null) { _context.Matches.Remove(match); await _context.SaveChangesAsync(); }
            return Ok();
        }
        
        [HttpPost("add-tournament")]
        public async Task<IActionResult> AddTournament([FromBody] CreateTournamentDto request)
        {
            var tournament = new Tournament
            {
                Name = request.Name, Category = request.Category,
                StartDate = request.StartDate, EndDate = request.EndDate, IsActive = true
            };
            _context.Tournaments.Add(tournament);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Turnuva eklendi.", id = tournament.Id });
        }
        
        [HttpPut("update-student-profile")]
        public async Task<IActionResult> UpdateStudentProfile([FromBody] UpdateStudentStatsDto model)
        {
            var student = await _userManager.FindByIdAsync(model.StudentId);
            if (student == null) return NotFound("Öğrenci bulunamadı.");

            student.Height = model.Height;
            student.Weight = model.Weight;
            student.BirthDate = model.BirthDate;
            if (Enum.TryParse<TennisLevel>(model.Level, out var level)) student.Level = level;
            if (Enum.TryParse<DominantHand>(model.Hand, out var hand)) student.DominantHand = hand;
            student.ConcurrencyStamp = Guid.NewGuid().ToString();

            await _userManager.UpdateAsync(student);
            return Ok(new { message = "Güncellendi." });
        }

        // ==========================================
        // KOÇ SEÇİM LİSTESİ (Frontend Dropdown)
        // ==========================================
        [HttpGet("select-list")]
        [AllowAnonymous] 
        public async Task<IActionResult> GetCoachesForSelection()
        {
            var coaches = await _context.Coaches
                .Include(c => c.AppUser)
                .Select(c => new { Id = c.Id, FullName = c.AppUser.FirstName + " " + c.AppUser.LastName })
                .ToListAsync();
            return Ok(coaches);
        }
        
        // ==========================================
        // 8. KOÇ TARAFINDAN MANUEL DERS EKLEME
        // ==========================================
        [HttpPost("manual-book")]
        public async Task<IActionResult> ManualBookStudent([FromBody] CoachManualBookingDto request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var coach = await _context.Coaches.FirstOrDefaultAsync(c => c.AppUserId == userId);
            if (coach == null) return Unauthorized("Koç bulunamadı.");

            // 1. Öğrenciyi Bul
            var student = await _userManager.FindByIdAsync(request.StudentId);
            if (student == null) return NotFound("Öğrenci bulunamadı.");

            // 2. Zaman Ayarı (Türkiye Saati -> UTC)
            DateTime startUtc, endUtc;
            try 
            {
                // Windows ve Linux uyumlu TimeZone alma
                string timeZoneId = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows) 
                    ? "Turkey Standard Time" : "Europe/Istanbul";
                TimeZoneInfo trTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

                // Frontend'den gelen tarih yerel ise UTC'ye çeviriyoruz
                startUtc = TimeZoneInfo.ConvertTimeToUtc(request.StartTime, trTimeZone);
                endUtc = TimeZoneInfo.ConvertTimeToUtc(request.EndTime, trTimeZone);
            }
            catch
            {
                // Fallback (Manuel -3 saat)
                startUtc = request.StartTime.AddHours(-3);
                endUtc = request.EndTime.AddHours(-3);
            }

            if (startUtc < DateTime.UtcNow) return BadRequest("Geçmiş zamana ders ekleyemezsiniz.");

            // 3. ÇAKIŞMA KONTROLÜ (Öğrenci o saatte dolu mu?)
            var studentBusyPrivate = await _context.LessonBookings
                .AnyAsync(b => b.StudentId == student.Id && b.Status != BookingStatus.Cancelled && 
                               ((b.StartTime < endUtc && b.EndTime > startUtc)));

            var studentBusyGroup = await _context.GroupLessonRegistrations
                .Include(r => r.GroupLesson)
                .AnyAsync(r => r.StudentId == student.Id && 
                               ((r.GroupLesson.StartTime < endUtc && r.GroupLesson.EndTime > startUtc)));

            if (studentBusyPrivate || studentBusyGroup)
            {
                return BadRequest("Öğrencinin bu saat aralığında başka bir dersi var.");
            }

            // 4. KOÇ MÜSAİTLİK KONTROLÜ (İsteğe bağlı, koç kendi üstüne çakışan ders yazabilir mi? 
            // Genelde yazabilmeli (manuel override), o yüzden burayı esnek bırakıyoruz ama uyarı olarak frontend'e dönebiliriz.)

            // 5. İŞLEM (Transaction ile Kredi Düşme ve Ders Oluşturma)
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (request.Type == "Private")
                {
                    // --- BİREYSEL DERS ---
                    if (student.LessonCredits < 1) 
                        return BadRequest("Öğrencinin yeterli BİREYSEL ders kredisi yok.");

                    // Kredi Düş
                    student.LessonCredits -= 1;
                    await _userManager.UpdateAsync(student);

                    // Ders Oluştur
                    var booking = new LessonBooking
                    {
                        CoachId = coach.Id,
                        StudentId = student.Id,
                        StartTime = startUtc,
                        EndTime = endUtc,
                        Status = BookingStatus.Confirmed, // Direkt onaylı
                        CreatedDate = DateTime.UtcNow
                    };
                    _context.LessonBookings.Add(booking);
                }
                else if (request.Type == "Group")
                {
                    // --- GRUP DERSİ ---
                    if (student.GroupCredits < 1)
                        return BadRequest("Öğrencinin yeterli GRUP ders kredisi yok.");

                    // Kredi Düş
                    student.GroupCredits -= 1;
                    await _userManager.UpdateAsync(student);

                    // Yeni Bir Grup Dersi Oluştur
                    var groupLesson = new GroupLesson
                    {
                        CoachId = coach.Id,
                        Title = request.Title ?? "Grup Dersi (Manuel)",
                        StartTime = startUtc,
                        EndTime = endUtc,
                        Capacity = 4, // Varsayılan kapasite
                        CreditCost = 1,
                        IsActive = true
                    };
                    _context.GroupLessons.Add(groupLesson);
                    await _context.SaveChangesAsync(); // ID oluşması için kaydet

                    // Öğrenciyi Bu Gruba Kaydet
                    var registration = new GroupLessonRegistration
                    {
                        GroupLessonId = groupLesson.Id,
                        StudentId = student.Id,
                        RegistrationDate = DateTime.UtcNow
                    };
                    _context.GroupLessonRegistrations.Add(registration);
                }
                else
                {
                    return BadRequest("Geçersiz ders tipi.");
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { message = "Ders başarıyla oluşturuldu ve kredi tahsil edildi." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, "İşlem sırasında hata oluştu: " + ex.Message);
            }
        }
    }
}