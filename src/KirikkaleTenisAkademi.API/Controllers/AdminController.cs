using KirikkaleTenisAkademi.Domain.Entities;
using KirikkaleTenisAkademi.Domain.Enums;
using KirikkaleTenisAkademi.Infrastructure.Persistence;
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
            // 1. Validasyonlar
            if (await _context.Users.AnyAsync(u => u.Email == request.Email))
                return BadRequest("Bu e-posta adresi zaten kullanılıyor.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 2. Kullanıcıyı Oluştur (AppUser)
                var newUserId = Guid.NewGuid().ToString();
                var user = new AppUser 
                { 
                    Id = newUserId,
                    UserName = request.Email, 
                    NormalizedUserName = request.Email.ToUpperInvariant(),
                    Email = request.Email, 
                    NormalizedEmail = request.Email.ToUpperInvariant(),
                    
                    // DÜZELTME: Artık FullName yok, parçalı kaydediyoruz
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    
                    LessonCredits = 0,
                    EmailConfirmed = true,
                    SecurityStamp = Guid.NewGuid().ToString()
                };

                // Şifreyi Hashle
                var passwordHasher = new PasswordHasher<AppUser>();
                user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

                _context.Users.Add(user);

                // 3. Rolü Bağla
                var coachRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Coach");
                if (coachRole != null)
                {
                    _context.UserRoles.Add(new IdentityUserRole<string>
                    {
                        UserId = newUserId,
                        RoleId = coachRole.Id
                    });
                }

                // 4. Koç Profilini Oluştur (Domain Entity)
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

                // 5. Hepsini Kaydet
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
            
            var totalBookings = await _context.LessonBookings
                .CountAsync(b => b.Status == BookingStatus.Confirmed);
            
            var completedLessons = await _context.LessonBookings
                .CountAsync(b => b.Status == BookingStatus.Completed);
        
            return Ok(new 
            {
                TotalUsers = totalUsers,
                TotalCoaches = totalCoaches,
                TotalBookings = totalBookings,
                CompletedLessons = completedLessons
            });
        }
        
        // ==========================================
        // 3. TÜM KULLANICILARI GETİR
        // ==========================================
        [HttpGet("users")]
        public async Task<ActionResult<IEnumerable<object>>> GetAllUsers()
        {
            var users = await _context.Users.AsNoTracking().ToListAsync();
            var userList = new List<object>();
        
            foreach (var user in users)
            {
                var roles = await _context.UserRoles
                    .Where(ur => ur.UserId == user.Id)
                    .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
                    .ToListAsync();

                userList.Add(new
                {
                    user.Id,
                    // DÜZELTME: FullName olmadığı için manuel birleştiriyoruz
                    // Frontend 'fullName' beklediği için bu ismi koruyoruz
                    FullName = $"{user.FirstName} {user.LastName}", 
                    user.Email,
                    user.UserName,
                    user.LessonCredits,
                    Roles = roles 
                });
            }
        
            return Ok(userList);
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
                // 1. Öğrenciyse rezervasyonlarını temizle
                var studentBookings = await _context.LessonBookings
                    .Where(b => b.StudentId == id)
                    .ToListAsync();
                
                if (studentBookings.Any())
                {
                    _context.LessonBookings.RemoveRange(studentBookings);
                }

                // 2. Koç ise profilini ve derslerini sil
                var coach = await _context.Coaches.FirstOrDefaultAsync(c => c.AppUserId == id);
                if (coach != null)
                {
                    var coachBookings = await _context.LessonBookings
                        .Where(b => b.CoachId == coach.Id)
                        .ToListAsync();
                    
                    _context.LessonBookings.RemoveRange(coachBookings);
                    
                    var unavailabilities = await _context.CoachUnavailabilities
                        .Where(u => u.CoachId == coach.Id)
                        .ToListAsync();
                    
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

                return Ok(new { message = "Kullanıcı silindi." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Silme hatası.", error = ex.Message });
            }
        }
    }
}