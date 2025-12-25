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

            // ==============================================================================
            // 1. SAAT DÜZELTMESİ (Timezone Fix)
            // ==============================================================================
            DateTime startUtc;
            DateTime endUtc;

            try 
            {
                // Sunucu işletim sistemine göre TimeZone ID belirle
                string timeZoneId = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows) 
                    ? "Turkey Standard Time" 
                    : "Europe/Istanbul";

                TimeZoneInfo trTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

                // Gelen saati Türkiye saati kabul et ve UTC'ye çevir
                startUtc = TimeZoneInfo.ConvertTimeToUtc(request.StartTime, trTimeZone);
                
                // Bitiş saati için de aynısını yap (Eğer request'te geliyorsa)
                // Eğer request.EndTime boş geliyorsa veya 1 saatlik standartsa: startUtc.AddHours(1) diyebiliriz.
                // Mevcut kodunuzda request.EndTime kullanıldığı için onu da çeviriyoruz:
                endUtc = TimeZoneInfo.ConvertTimeToUtc(request.EndTime, trTimeZone);
            }
            catch
            {
                // TimeZone bulunamazsa manuel -3 saat yap (Yedek plan)
                startUtc = request.StartTime.AddHours(-3);
                endUtc = request.EndTime.AddHours(-3);
            }

            // ARTIK AŞAĞIDAKİ TÜM MANTIKTA 'startUtc' ve 'endUtc' KULLANACAĞIZ.

            // Validasyon 1: Geçmişe blok konamaz (UtcNow ile kıyasla)
            if (startUtc < DateTime.UtcNow)
                return BadRequest("Geçmiş bir tarihi kapatamazsınız.");

            // Validasyon 2: Bitiş, Başlangıçtan sonra olmalı
            if (endUtc <= startUtc)
                return BadRequest("Bitiş saati başlangıçtan ileri olmalı.");

            // Validasyon 3: O saatte onaylı ders var mı? (UTC ile kontrol)
            var hasLesson = await _context.LessonBookings
                .AnyAsync(b => b.CoachId == coach.Id 
                               && b.Status == BookingStatus.Confirmed
                               && b.StartTime < endUtc 
                               && b.EndTime > startUtc);

            if (hasLesson)
                return BadRequest("Bu saat aralığında zaten onaylanmış bir dersiniz var. Önce dersi iptal etmelisiniz.");

            // Kaydet (UTC olarak)
            var unavailable = new CoachUnavailability
            {
                CoachId = coach.Id,
                StartTime = startUtc, // UTC
                EndTime = endUtc,     // UTC
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
                .Where(u => u.CoachId == coach.Id && u.EndTime > DateTime.UtcNow) // DÜZELTME: UtcNow kullandık
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

            // Güvenlik: Başkasının bloğunu silemesin
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
        
        // ==========================================
        // KOÇUN TAM TAKVİMİ (Dersler + Bloklar)
        // ==========================================
        [HttpGet("my-full-schedule")]
        public async Task<IActionResult> GetMyFullSchedule()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var coach = await _context.Coaches.FirstOrDefaultAsync(c => c.AppUserId == userId);
            if (coach == null) return Unauthorized();

            // 1. DERSLER (Öğrenci İsimleriyle)
            var lessons = await _context.LessonBookings
                .Include(b => b.Student) // Öğrenci ismini almak için
                .Where(b => b.CoachId == coach.Id && b.Status != BookingStatus.Cancelled)
                .Select(b => new 
                {
                    Id = b.Id,
                    StartTime = b.StartTime,
                    EndTime = b.EndTime,
                    Type = "Lesson",
                    Title = b.Student.FirstName + " " + b.Student.LastName, // Öğrenci Adı
                    StudentId = b.StudentId
                })
                .ToListAsync();

            // 2. BLOKLAR (Koçun Kapattığı Saatler)
            var blocks = await _context.CoachUnavailabilities
                .Where(u => u.CoachId == coach.Id)
                .Select(u => new 
                {
                    Id = u.Id,
                    StartTime = u.StartTime,
                    EndTime = u.EndTime,
                    Type = "Block",
                    Title = u.Reason ?? "Kapalı" // Sebep
                })
                .ToListAsync();

            // 3. BİRLEŞTİR
            var fullSchedule = new List<object>();
            fullSchedule.AddRange(lessons);
            fullSchedule.AddRange(blocks);

            return Ok(fullSchedule);
        }
        
        // ==========================================
        // KREDİ İADESİ
        // ==========================================
        [HttpPost("grant-credit")]
        [Authorize(Roles = "Coach")]
        public async Task<IActionResult> GrantCredit([FromBody] GrantCreditRequest request)
        {
            var student = await _userManager.FindByIdAsync(request.StudentId);
            if (student == null) return NotFound("Öğrenci bulunamadı.");

            student.LessonCredits += request.Amount;
            await _userManager.UpdateAsync(student);

            return Ok(new { message = $"{student.FirstName} isimli öğrenciye {request.Amount} kredi başarıyla yüklendi." });
        }
        
        // ==========================================
        // ÖĞRENCİLERİ LİSTELE
        // ==========================================
        [HttpGet("my-students-portfolio")]
        public async Task<IActionResult> GetMyStudentsPortfolio()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
    
            // Koç kontrolü
            var coach = await _context.Coaches.FirstOrDefaultAsync(c => c.AppUserId == userId);
            if (coach == null) return Unauthorized();

            // SORGULAMA: Tüm 'Student' rolündeki kullanıcıları çeker ve DTO'ya doldurur.
            var students = await (from user in _context.Users
                    join userRole in _context.UserRoles on user.Id equals userRole.UserId
                    join role in _context.Roles on userRole.RoleId equals role.Id
                    where role.Name == "Student"
            
                    // BURADA GÜNCELLEME YAPTIK: Doğrudan MyStudentDto'ya çeviriyoruz
                    select new MyStudentDto
                    {
                        StudentId = user.Id,
                        FullName = user.FirstName + " " + user.LastName,
                        Email = user.Email,
                        ProfileImageUrl = user.ProfileImageUrl,
                        CurrentCredits = user.LessonCredits,

                        // --- YENİ EKLENEN ALANLAR ---
                        TCKN = user.TCKN,
                        Phone = user.PhoneNumber, // IdentityUser'dan gelen telefon
                        BirthDate = user.BirthDate,
                
                        // Fiziksel Bilgiler
                        Height = user.Height,
                        Weight = user.Weight,
                
                        // Enum Değerlerini String'e Çevirme
                        // (Veritabanında sayı tutulur, okurken yazıya çeviriyoruz)
                        Level = user.Level.ToString(), 
                        Hand = user.DominantHand.ToString(),

                        // Acil Durum (İsim ve Numarayı birleştirip tek string yapabiliriz)
                        EmergencyContact = user.EmergencyContactName + " (" + user.EmergencyContactPhone + ")"
                    })
                .ToListAsync();

            return Ok(students);
        }
        
        // CoachController.cs içine ekle:

        // 1. AKTİF TURNUVALARI GETİR (Dropdown için)
        [HttpGet("tournaments")]
        public async Task<IActionResult> GetActiveTournaments()
        {
            var tournaments = await _context.Tournaments
                .Where(t => t.IsActive)
                .Select(t => new TournamentSelectDto { Id = t.Id, Name = t.Name })
                .ToListAsync();
                
            return Ok(tournaments);
        }

        // 2. MAÇ EKLE
        [HttpPost("add-match")]
        public async Task<IActionResult> AddMatch([FromBody] MatchDto request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var coach = await _context.Coaches.FirstOrDefaultAsync(c => c.AppUserId == userId);
            if (coach == null) return Unauthorized("Koç bulunamadı.");

            var match = new Match
            {
                TournamentId = request.TournamentId,
                MatchDate = request.MatchDate,
                
                // Oyuncular
                Player1Id = request.StudentId, // Bizim öğrenci
                Player2ExternalName = request.OpponentName, // Dışarıdan rakip
                
                // Skorlar
                ScoreSet1 = request.ScoreSet1,
                ScoreSet2 = request.ScoreSet2,
                ScoreSet3 = request.ScoreSet3,
                
                // Koç Bilgileri
                RefereeCoachId = coach.Id,
                CoachNotes = request.CoachNotes,
                
                // Kazanan Mantığı: Eğer 'IsWinner' true ise kazanan bizim öğrenci
                WinnerId = request.IsWinner ? request.StudentId : null 
            };

            _context.Matches.Add(match);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Maç başarıyla eklendi." });
        }

        // 3. ÖĞRENCİNİN MAÇLARINI GETİR
        [HttpGet("student-matches/{studentId}")]
        public async Task<IActionResult> GetStudentMatches(string studentId)
        {
            var matches = await _context.Matches
                .Include(m => m.Tournament) // Turnuva ismini çekmek için
                .Where(m => m.Player1Id == studentId || m.Player2Id == studentId) // Öğrenci P1 veya P2 olabilir
                .OrderByDescending(m => m.MatchDate)
                .Select(m => new MatchDto
                {
                    Id = m.Id,
                    TournamentId = m.TournamentId,
                    TournamentName = m.Tournament != null ? m.Tournament.Name : "Bilinmiyor",
                    MatchDate = m.MatchDate,
                    
                    // Rakip ismini bulma mantığı
                    OpponentName = m.Player1Id == studentId ? m.Player2ExternalName : "Rakip", 
                    
                    ScoreSet1 = m.ScoreSet1,
                    ScoreSet2 = m.ScoreSet2,
                    ScoreSet3 = m.ScoreSet3,
                    CoachNotes = m.CoachNotes,
                    
                    // Eğer WinnerId öğrencinin ID'si ise kazanmıştır
                    IsWinner = m.WinnerId == studentId
                })
                .ToListAsync();

            return Ok(matches);
        }

        // 4. MAÇ SİL
        [HttpDelete("delete-match/{id}")]
        public async Task<IActionResult> DeleteMatch(int id)
        {
            var match = await _context.Matches.FindAsync(id);
            if (match == null) return NotFound();
            _context.Matches.Remove(match);
            await _context.SaveChangesAsync();
            return Ok();
        }
        
        // CoachController.cs içine ekle:

        [HttpPost("add-tournament")]
        public async Task<IActionResult> AddTournament([FromBody] CreateTournamentDto request)
        {
            if (string.IsNullOrEmpty(request.Name)) return BadRequest("Turnuva adı zorunludur.");

            var tournament = new Tournament
            {
                Name = request.Name,
                Category = request.Category,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                IsActive = true // Eklenen turnuva varsayılan aktif olsun
            };

            _context.Tournaments.Add(tournament);
            await _context.SaveChangesAsync();

            // Frontend'e eklenen turnuvanın ID'sini dönelim ki otomatik seçtirebilelim
            return Ok(new { message = "Turnuva eklendi.", id = tournament.Id });
        }
        
        // CoachController.cs

        [HttpPut("update-student-profile")]
        public async Task<IActionResult> UpdateStudentProfile([FromBody] UpdateStudentStatsDto model)
        {
            // 1. Öğrenciyi Bul
            var student = await _userManager.FindByIdAsync(model.StudentId);
            if (student == null) return NotFound("Öğrenci bulunamadı.");

            // 2. Sadece Fiziksel Verileri Güncelle
            student.Height = model.Height;
            student.Weight = model.Weight;
            student.BirthDate = model.BirthDate;

            // 3. Enum Dönüşümleri (Boş gelirse hata vermesin diye kontrol ediyoruz)
            if (!string.IsNullOrEmpty(model.Level) && 
                Enum.TryParse<Domain.Enums.TennisLevel>(model.Level, out var level)) 
            {
                student.Level = level;
            }

            if (!string.IsNullOrEmpty(model.Hand) && 
                Enum.TryParse<Domain.Enums.DominantHand>(model.Hand, out var hand)) 
            {
                student.DominantHand = hand;
            }
    
            // Not: BackhandStyle koç tarafından güncellenmiyorsa buraya eklemene gerek yok.

            // 4. Kaydet
            var result = await _userManager.UpdateAsync(student);

            if (result.Succeeded)
            {
                return Ok(new { message = "Öğrenci bilgileri güncellendi." });
            }
    
            return BadRequest("Güncelleme başarısız.");
        }
    }
}