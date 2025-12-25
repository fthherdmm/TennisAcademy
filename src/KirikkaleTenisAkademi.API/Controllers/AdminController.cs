using KirikkaleTenisAkademi.Domain.Entities;
using KirikkaleTenisAkademi.Domain.Enums;
using KirikkaleTenisAkademi.Infrastructure.Persistence;
using KirikkaleTenisAkademi.Web.Models; // DTO'nun olduğu namespace (veya Application.DTOs)
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KirikkaleTenisAkademi.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")] // 🔒 Sadece Admin Girebilir
    public class AdminController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly AppDbContext _context;
        
        public AdminController(UserManager<AppUser> userManager, AppDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }
        
        // ==========================================
        // 1. KOÇ EKLEME
        // ==========================================
        public class CreateCoachRequest
        {
            public string Email { get; set; }
            public string Password { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public string Specialty { get; set; }
            public string Bio { get; set; }
            public string ImageUrl { get; set; }
        }
        
        [HttpPost("create-coach")]
        public async Task<IActionResult> CreateCoach(CreateCoachRequest request)
        {
            // Validasyon
            if (await _context.Users.AnyAsync(u => u.Email == request.Email))
                return BadRequest("Bu e-posta adresi zaten kullanılıyor.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Kullanıcıyı Oluştur (AppUser)
                var newUserId = Guid.NewGuid().ToString();
                var user = new AppUser 
                { 
                    Id = newUserId,
                    UserName = request.Email, 
                    NormalizedUserName = request.Email.ToUpperInvariant(),
                    Email = request.Email, 
                    NormalizedEmail = request.Email.ToUpperInvariant(),
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    LessonCredits = 0,
                    EmailConfirmed = true,
                    SecurityStamp = Guid.NewGuid().ToString(),
                    
                    // Koçlar için varsayılan değerler
                    Level = TennisLevel.Pro,
                    RegistrationDate = DateTime.UtcNow
                };

                var passwordHasher = new PasswordHasher<AppUser>();
                user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

                _context.Users.Add(user);

                // 2. Rolü Bağla
                var coachRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Coach");
                if (coachRole != null)
                {
                    _context.UserRoles.Add(new IdentityUserRole<string> { UserId = newUserId, RoleId = coachRole.Id });
                }

                // 3. Koç Profilini Oluştur (Domain Entity)
                var coach = new Coach
                {
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    Specialty = request.Specialty,
                    Bio = request.Bio,
                    ImageUrl = request.ImageUrl,
                    AppUserId = newUserId
                };
                
                _context.Coaches.Add(coach);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            
                return Ok(new { message = "Koç başarıyla oluşturuldu." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Koç oluşturulurken hata.", error = ex.Message });
            }
        }
        
        // ==========================================
        // 2. DASHBOARD İSTATİSTİKLERİ
        // ==========================================
        [HttpGet("stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
            var totalUsers = await _context.Users.CountAsync();
            var totalCoaches = await _context.Coaches.CountAsync();
            
            var totalBookings = await _context.LessonBookings.CountAsync(b => b.Status == BookingStatus.Confirmed);
            var completedLessons = await _context.LessonBookings.CountAsync(b => b.Status == BookingStatus.Completed);
        
            return Ok(new 
            {
                TotalUsers = totalUsers,
                TotalCoaches = totalCoaches,
                TotalBookings = totalBookings,
                CompletedLessons = completedLessons
            });
        }
        
        // ==========================================
        // 3. TÜM KULLANICILARI GETİR (GÜNCELLENDİ) 🚀
        // ==========================================
        [HttpGet("users")]
        public async Task<ActionResult<IEnumerable<UserProfileDto>>> GetAllUsers()
        {
            // Kullanıcıları çekiyoruz
            var users = await _context.Users.AsNoTracking().ToListAsync();
            var userProfileList = new List<UserProfileDto>();
        
            foreach (var user in users)
            {
                // Kullanıcının rollerini çekiyoruz
                var roles = await _context.UserRoles
                    .Where(ur => ur.UserId == user.Id)
                    .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
                    .ToListAsync();

                // UserProfileDto'yu detaylı bilgilerle dolduruyoruz
                userProfileList.Add(new UserProfileDto
                {
                    Id = user.Id,
                    Roles = roles,

                    // Kişisel
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    UserName = user.UserName,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber, // Admin panelinde görünecek
                    TCKN = user.TCKN,               // Admin panelinde görünecek
                    BirthDate = user.BirthDate,

                    // Fiziksel & Teknik (Detay butonu için)
                    Height = user.Height,
                    Weight = user.Weight,
                    
                    // Enum'ları String'e çeviriyoruz
                    Level = user.Level.ToString(),
                    DominantHand = user.DominantHand.ToString(),
                    BackhandStyle = user.BackhandStyle.ToString(),

                    // Acil Durum
                    EmergencyContactName = user.EmergencyContactName,
                    EmergencyContactPhone = user.EmergencyContactPhone
                });
            }
        
            return Ok(userProfileList);
        }
        
        // ==========================================
        // 4. KULLANICI SİL
        // ==========================================
        [HttpDelete("users/{id}")]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound("Kullanıcı bulunamadı.");
        
            if (user.UserName == User.Identity?.Name)
                return BadRequest("Kendinizi silemezsiniz!");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Öğrenci rezervasyonlarını sil
                var studentBookings = await _context.LessonBookings.Where(b => b.StudentId == id).ToListAsync();
                if (studentBookings.Any()) _context.LessonBookings.RemoveRange(studentBookings);

                // 2. Koç ise profilini ve derslerini sil
                var coach = await _context.Coaches.FirstOrDefaultAsync(c => c.AppUserId == id);
                if (coach != null)
                {
                    var coachBookings = await _context.LessonBookings.Where(b => b.CoachId == coach.Id).ToListAsync();
                    _context.LessonBookings.RemoveRange(coachBookings);
                    
                    var unavailabilities = await _context.CoachUnavailabilities.Where(u => u.CoachId == coach.Id).ToListAsync();
                    _context.CoachUnavailabilities.RemoveRange(unavailabilities);

                    _context.Coaches.Remove(coach);
                }

                // 3. Rolleri sil
                var userRoles = await _context.UserRoles.Where(ur => ur.UserId == id).ToListAsync();
                _context.UserRoles.RemoveRange(userRoles);

                // 4. Kullanıcıyı sil
                _context.Users.Remove(user);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { message = "Kullanıcı başarıyla silindi." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Silme hatası.", error = ex.Message });
            }
        }
    }
}