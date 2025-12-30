using System.IdentityModel.Tokens.Jwt;
using KirikkaleTenisAkademi.Domain.Entities;
using KirikkaleTenisAkademi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;
using KirikkaleTenisAkademi.Application.DTOs.Auth;
using KirikkaleTenisAkademi.Web.Models;
using Microsoft.AspNetCore.Authorization;
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
                TCKN = request.TCKN,
                PhoneNumber = request.PhoneNumber,
                BirthDate = request.BirthDate,
                LessonCredits = 0,
                EmailConfirmed = false,
                SecurityStamp = Guid.NewGuid().ToString(),
                ConcurrencyStamp = Guid.NewGuid().ToString(),
                Level = Domain.Enums.TennisLevel.Beginner,
                RegistrationDate = DateTime.UtcNow
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

        // AuthController.cs içine eklenecek metotlar:
        [HttpGet("profile")]
        [Authorize] // Sadece giriş yapanlar
        public async Task<IActionResult> GetProfile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            // Veritabanından kullanıcıyı tüm detaylarıyla çekiyoruz
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound("Kullanıcı bulunamadı.");

            // Entity -> DTO Dönüşümü
            var profile = new UserProfileDto
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                UserName = user.UserName,
                PhoneNumber = user.PhoneNumber,
                TCKN = user.TCKN,
                BirthDate = user.BirthDate,
                
                // Fiziksel
                Height = user.Height,
                Weight = user.Weight,
                
                // Enum'ları string olarak frontend'e gönderiyoruz
                Level = user.Level.ToString(),
                DominantHand = user.DominantHand.ToString(),
                BackhandStyle = user.BackhandStyle.ToString(),
                
                // Acil Durum
                EmergencyContactName = user.EmergencyContactName,
                EmergencyContactPhone = user.EmergencyContactPhone,
                LessonCredits = user.LessonCredits
            };

            return Ok(profile);
        }

        [HttpPut("profile")]
        [Authorize]
        public async Task<IActionResult> UpdateProfile([FromBody] UserProfileDto model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            // Gelen verileri veritabanı nesnesine aktarıyoruz
            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.PhoneNumber = model.PhoneNumber;
            user.BirthDate = model.BirthDate;
            user.Height = model.Height;
            user.Weight = model.Weight;
            user.EmergencyContactName = model.EmergencyContactName;
            user.EmergencyContactPhone = model.EmergencyContactPhone;

            // String gelen Enum değerlerini güvenli şekilde çeviriyoruz
            if (Enum.TryParse<Domain.Enums.TennisLevel>(model.Level, out var level)) 
                user.Level = level;
                
            if (Enum.TryParse<Domain.Enums.DominantHand>(model.DominantHand, out var hand)) 
                user.DominantHand = hand;
                
            if (Enum.TryParse<Domain.Enums.BackhandStyle>(model.BackhandStyle, out var back)) 
                user.BackhandStyle = back;

            // Güncelleme işlemi
            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                return Ok(new { message = "Profil güncellendi." });
            }

            return BadRequest(result.Errors);
        }
    }

    // Register için Gelen Veri Modeli
    public class RegisterRequest
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        // Yeni Eklenen Alanlar
        public string TCKN { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public DateTime BirthDate { get; set; }

        public DateTime RegistrationDate = DateTime.UtcNow;
    }
}