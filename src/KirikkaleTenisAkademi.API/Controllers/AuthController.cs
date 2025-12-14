using System.IdentityModel.Tokens.Jwt;
using KirikkaleTenisAkademi.Domain.Entities;
using KirikkaleTenisAkademi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;
using KirikkaleTenisAkademi.Application.DTOs.Auth;
using Microsoft.IdentityModel.Tokens;

namespace KirikkaleTenisAkademi.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly AppDbContext _context; // Veritabanına direkt erişim için

        public AuthController(UserManager<AppUser> userManager, IConfiguration configuration, AppDbContext context)
        {
            _userManager = userManager;
            _configuration = configuration;
            _context = context;
        }

        // --- REGISTER (KAYIT OL) ---
        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequest request)
        {
            // 1. Validasyonlar
            // Not: _context.Users, veritabanındaki "AppUser" tablosudur.
            if (await _context.Users.AnyAsync(u => u.UserName == request.UserName))
                return BadRequest("Bu kullanıcı adı zaten alınmış.");

            if (await _context.Users.AnyAsync(u => u.Email == request.Email))
                return BadRequest("Bu e-posta adresi zaten kullanılıyor.");

            // 2. Kullanıcı Nesnesini Hazırla (ID'yi biz veriyoruz)
            var newUserId = Guid.NewGuid().ToString();
            
            // DÜZELTME: ApplicationUser yerine AppUser kullanıyoruz.
            var user = new AppUser
            {
                Id = newUserId, 
                UserName = request.UserName,
                NormalizedUserName = request.UserName.ToUpperInvariant(), 
                Email = request.Email,
                NormalizedEmail = request.Email.ToUpperInvariant(),
                FirstName = request.FirstName,
                LastName = request.LastName,
                LessonCredits = 0,
                EmailConfirmed = false,
                SecurityStamp = Guid.NewGuid().ToString(),
                ConcurrencyStamp = Guid.NewGuid().ToString()
            };

            // 3. Şifreyi Manuel Hash'le
            // DÜZELTME: Burası da AppUser olmalı
            var passwordHasher = new PasswordHasher<AppUser>();
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

            // 4. VERİTABANI İŞLEMİ (Tek Seferde)
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // A. Kullanıcıyı ekle
                _context.Users.Add(user); // Artık user nesnesi "AppUser" tipinde olduğu için hata vermez.
                
                // B. Rolü bul ve kullanıcıya bağla
                var studentRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Student");
                if (studentRole != null)
                {
                    _context.UserRoles.Add(new IdentityUserRole<string>
                    {
                        UserId = newUserId, 
                        RoleId = studentRole.Id
                    });
                }

                // C. Hepsini TEK SEFERDE kaydet
                await _context.SaveChangesAsync();
                
                // D. İşlemi Onayla
                await transaction.CommitAsync();

                return Ok(new { message = "Kullanıcı başarıyla oluşturuldu." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Kayıt hatası.", error = ex.InnerException?.Message ?? ex.Message });
            }
        }

        // --- LOGIN (GİRİŞ YAP) ---
        // (Login kodun buradaydı, aynen kalabilir veya aşağıya ekleyebilirsin)
        // Eğer Login kodun silindiyse söyle, onu da atayım.
        // Gerekli kütüphaneleri eklediğinden emin ol:
        // using System.IdentityModel.Tokens.Jwt;
        // using System.Security.Claims;
        // using Microsoft.IdentityModel.Tokens;
        // using System.Text;

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            // 1. Kullanıcıyı Email ile Bul (Büyük/Küçük harf duyarlılığını kaldırmak için ToLower yapabiliriz ama şimdilik direkt arayalım)
            // AsNoTracking performans içindir.
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null)
            {
                return Unauthorized(new { message = "Hatalı email veya şifre." });
            }

            // 2. Şifreyi Doğrula (Manuel Hash Kontrolü)
            var passwordHasher = new PasswordHasher<AppUser>();
            var verificationResult = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

            if (verificationResult == PasswordVerificationResult.Failed)
            {
                return Unauthorized(new { message = "Hatalı email veya şifre." });
            }

            // 3. Kullanıcının Rollerini Çek
            // IdentityUserRole tablosundan bu UserID'ye ait rolleri bul, sonra Roles tablosundan isimlerini al.
            var userRoles = await _context.UserRoles
                .Where(ur => ur.UserId == user.Id)
                .Join(_context.Roles, 
                      ur => ur.RoleId, 
                      r => r.Id, 
                      (ur, r) => r.Name)
                .ToListAsync();

            // 4. Token Oluştur (JWT)
            var authClaims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.UserName ?? ""),
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            // Rolleri Token'a ekle
            foreach (var role in userRoles)
            {
                authClaims.Add(new Claim(ClaimTypes.Role, role));
            }

            // appsettings.json'dan okuyoruz
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = Encoding.UTF8.GetBytes(jwtSettings["Secret"]!);

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                expires: DateTime.Now.AddHours(3), // Token 3 saat geçerli
                claims: authClaims,
                signingCredentials: new SigningCredentials(new SymmetricSecurityKey(secretKey), SecurityAlgorithms.HmacSha256)
            );

            return Ok(new
            {
                token = new JwtSecurityTokenHandler().WriteToken(token),
                expiration = token.ValidTo,
                role = userRoles.FirstOrDefault() // Frontend genelde ilk rolü merak eder
            });
        }
        
        
        // --- PROFILE (PROFİL BİLGİSİ) ---
        // Frontend'in kredi bilgisini çekmesi için gerekli endpoint
        // AuthController.cs içine ekle:

        [HttpGet("profile")]
        [Microsoft.AspNetCore.Authorization.Authorize] // Sadece giriş yapmış olanlar görebilir
        public async Task<IActionResult> GetProfile()
        {
            // 1. Token'dan User ID'yi al
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("Kullanıcı bulunamadı.");
            }

            // 2. Veritabanından kullanıcıyı ve güncel kredisini çek
            var user = await _context.Users
                .AsNoTracking() // Sadece okuma yapıyoruz, hız kazandırır
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null) 
            {
                return NotFound("Kullanıcı veritabanında yok.");
            }

            // 3. Frontend'e gönder
            return Ok(new 
            {
                fullName = user.FirstName,
                email = user.Email,
                lessonCredits = user.LessonCredits // İşte burası bakiyeyi güncelleyen kısım
            });
        }
        
        
    }

    // Register için Gelen Veri Modeli
    public class RegisterRequest
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
    }
}