using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using KirikkaleTenisAkademi.Application.Interfaces; // E-posta servisi için
using KirikkaleTenisAkademi.Domain.Entities;
using KirikkaleTenisAkademi.Infrastructure.Persistence;
using KirikkaleTenisAkademi.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Net;
using KirikkaleTenisAkademi.Application.DTOs;
using Microsoft.AspNetCore.Identity.Data;
using LoginRequest = KirikkaleTenisAkademi.Application.DTOs.Auth.LoginRequest; // URL Encode için

namespace KirikkaleTenisAkademi.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly AppDbContext _context; // Veritabanına direkt erişim
        private readonly IEmailService _emailService; // 📧 Mail Servisi

        public AuthController(UserManager<AppUser> userManager, 
                              IConfiguration configuration, 
                              AppDbContext context,
                              IEmailService emailService)
        {
            _userManager = userManager;
            _configuration = configuration;
            _context = context;
            _emailService = emailService;
        }

        // --- REGISTER (KAYIT OL VE MAİL AT) ---
        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequest request)
        {
            // 1. Validasyonlar
            if (await _context.Users.AnyAsync(u => u.UserName == request.UserName))
                return BadRequest("Bu kullanıcı adı zaten alınmış.");

            if (await _context.Users.AnyAsync(u => u.Email == request.Email))
                return BadRequest("Bu e-posta adresi zaten kullanılıyor.");

            // 2. Kullanıcı Nesnesini Hazırla
            var newUserId = Guid.NewGuid().ToString();
            
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
                GroupCredits = 0,
                EmailConfirmed = false, // Mail onayı bekleniyor
                SecurityStamp = Guid.NewGuid().ToString(),
                ConcurrencyStamp = Guid.NewGuid().ToString(),
                Level = Domain.Enums.TennisLevel.Beginner,
                RegistrationDate = DateTime.UtcNow
            };

            // 3. Şifreyi Hash'le
            var passwordHasher = new PasswordHasher<AppUser>();
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // A. Kullanıcıyı ekle
                _context.Users.Add(user);
                
                // B. Rolü bağla
                var studentRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Student");
                if (studentRole != null)
                {
                    _context.UserRoles.Add(new IdentityUserRole<string>
                    {
                        UserId = newUserId, 
                        RoleId = studentRole.Id
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // ============================================================
                // 📧 4. TOKEN ÜRET VE MAİL GÖNDER
                // ============================================================
                // Not: user objesi Tracking modunda olmadığı için UserManager ile tekrar çekiyoruz (Token üretimi için şart)
                var createdUser = await _userManager.FindByIdAsync(newUserId);
                if (createdUser != null)
                {
                    var token = await _userManager.GenerateEmailConfirmationTokenAsync(createdUser);
                    var encodedToken = WebUtility.UrlEncode(token);

                    // Frontend URL'ini buraya yaz (Kendi portuna göre ayarla)
                    var frontendUrl = AppConstants.WebBaseUrl;
                    var confirmationLink = $"{frontendUrl}/confirm-email-page?userId={createdUser.Id}&token={encodedToken}";
                    
                    var subject = "Kırıkkale Tenis Akademi - Hesap Onayı";
                    var body = $@"
                        <h3>Aramıza Hoşgeldin {createdUser.FirstName}! 🎾</h3>
                        <p>Hesabını aktifleştirmek için lütfen aşağıdaki butona tıkla:</p>
                        <a href='{confirmationLink}' style='background-color:#2E7D32; color:white; padding:10px 20px; text-decoration:none; border-radius:5px;'>Hesabımı Doğrula</a>
                        <p>Link çalışmıyorsa: {confirmationLink}</p>";

                    await _emailService.SendEmailAsync(createdUser.Email, subject, body);
                }
                // ============================================================

                return Ok(new { message = "Kayıt başarılı! Lütfen e-posta adresinize gelen linke tıklayarak hesabınızı doğrulayın." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Kayıt hatası.", error = ex.InnerException?.Message ?? ex.Message });
            }
        }

        // --- EMAIL CONFIRM (MAİL ONAYLA) ---
        [HttpPost("confirm-email")]
        public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailDto model)
        {
            if (string.IsNullOrEmpty(model.UserId) || string.IsNullOrEmpty(model.Token))
                return BadRequest("Geçersiz istek.");

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null) return BadRequest("Kullanıcı bulunamadı.");

            var result = await _userManager.ConfirmEmailAsync(user, model.Token);
            
            if (result.Succeeded)
            {
                return Ok(new { message = "E-posta başarıyla doğrulandı! Artık giriş yapabilirsiniz." });
            }
            
            return BadRequest("Doğrulama başarısız. Token süresi dolmuş olabilir.");
        }

        // --- LOGIN (GİRİŞ YAP) ---
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            // Kullanıcıyı bul
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null) return Unauthorized(new { message = "Hatalı email veya şifre." });

            // 🛑 MAİL ONAYI KONTROLÜ
            if (!await _userManager.IsEmailConfirmedAsync(user))
                return Unauthorized(new { message = "Giriş yapmadan önce e-posta adresinizi doğrulamanız gerekmektedir." });

            // Şifre kontrolü
            if (!await _userManager.CheckPasswordAsync(user, request.Password))
                return Unauthorized(new { message = "Hatalı email veya şifre." });

            // Rolleri al
            var userRoles = await _userManager.GetRolesAsync(user);

            // Token oluştur
            var authClaims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.UserName ?? ""),
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            foreach (var role in userRoles)
            {
                authClaims.Add(new Claim(ClaimTypes.Role, role));
            }

            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = Encoding.UTF8.GetBytes(jwtSettings["Secret"]!);

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                expires: DateTime.Now.AddHours(3),
                claims: authClaims,
                signingCredentials: new SigningCredentials(new SymmetricSecurityKey(secretKey), SecurityAlgorithms.HmacSha256)
            );

            return Ok(new
            {
                token = new JwtSecurityTokenHandler().WriteToken(token),
                expiration = token.ValidTo,
                role = userRoles.FirstOrDefault()
            });
        }
        
        // --- PROFILE (PROFİL BİLGİSİ) ---
        [HttpGet("profile")]
        [Authorize]
        public async Task<IActionResult> GetProfile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound("Kullanıcı bulunamadı.");

            var profile = new UserProfileDto
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                UserName = user.UserName,
                PhoneNumber = user.PhoneNumber,
                TCKN = user.TCKN,
                BirthDate = user.BirthDate,
                Height = user.Height,
                Weight = user.Weight,
                Level = user.Level.ToString(),
                DominantHand = user.DominantHand.ToString(),
                BackhandStyle = user.BackhandStyle.ToString(),
                EmergencyContactName = user.EmergencyContactName,
                EmergencyContactPhone = user.EmergencyContactPhone,
                LessonCredits = user.LessonCredits,
                GroupCredits = user.GroupCredits
            };

            return Ok(profile);
        }

        [HttpPut("profile")]
        [Authorize]
        public async Task<IActionResult> UpdateProfile([FromBody] UserProfileDto model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound("Kullanıcı bulunamadı.");

            user.FirstName = model.FirstName ?? user.FirstName;
            user.LastName = model.LastName ?? user.LastName;
            user.PhoneNumber = model.PhoneNumber ?? user.PhoneNumber;
            user.BirthDate = model.BirthDate; 
            user.Height = model.Height;
            user.Weight = model.Weight;
            user.EmergencyContactName = model.EmergencyContactName;
            user.EmergencyContactPhone = model.EmergencyContactPhone;

            if (!string.IsNullOrEmpty(model.Level) && Enum.TryParse<Domain.Enums.TennisLevel>(model.Level, out var level)) 
                user.Level = level;
        
            if (!string.IsNullOrEmpty(model.DominantHand) && Enum.TryParse<Domain.Enums.DominantHand>(model.DominantHand, out var hand)) 
                user.DominantHand = hand;
        
            if (!string.IsNullOrEmpty(model.BackhandStyle) && Enum.TryParse<Domain.Enums.BackhandStyle>(model.BackhandStyle, out var back)) 
                user.BackhandStyle = back;

            user.ConcurrencyStamp = Guid.NewGuid().ToString();

            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                return Ok(new { message = "Profil güncellendi." });
            }

            return BadRequest(result.Errors);
        }
        
        // ==========================================
        // 🔥 ŞİFREMİ UNUTTUM (Link Gönder)
        // ==========================================
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest model)
        {
            if (string.IsNullOrEmpty(model.Email))
                return BadRequest("Lütfen e-posta adresinizi giriniz.");

            var user = await _userManager.FindByEmailAsync(model.Email);
            // Güvenlik gereği "Böyle bir kullanıcı yok" demek yerine "Eğer kayıtlıysa mail gönderildi" demek daha doğrudur
            // ama geliştirme aşamasında kolaylık olsun diye şimdilik direkt hatayı dönüyoruz.
            if (user == null) 
                return BadRequest("Bu e-posta adresiyle kayıtlı bir kullanıcı bulunamadı.");

            // Token oluştur
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = WebUtility.UrlEncode(token);

            // Frontend'deki Şifre Sıfırlama Sayfasının Linki
            var frontendUrl = AppConstants.WebBaseUrl; 
            var resetLink = $"{frontendUrl}/reset-password?email={model.Email}&token={encodedToken}";

            var subject = "Kırıkkale Tenis Akademi - Şifre Sıfırlama";
            var body = $@"
                <h3>Şifreni mi unuttun? 🔒</h3>
                <p>Sorun değil, aşağıdaki linke tıklayarak yeni şifreni belirleyebilirsin:</p>
                <a href='{resetLink}' style='background-color:#d32f2f; color:white; padding:10px 20px; text-decoration:none; border-radius:5px;'>Şifremi Sıfırla</a>
                <p>Bu işlemi sen yapmadıysan, bu maili görmezden gelebilirsin.</p>";

            await _emailService.SendEmailAsync(user.Email, subject, body);

            return Ok(new { message = "Şifre sıfırlama bağlantısı e-posta adresinize gönderildi." });
        }

        // ==========================================
        // 🔥 ŞİFREYİ SIFIRLA (Yeni Şifreyi Kaydet)
        // ==========================================
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest model)
        {
            if (string.IsNullOrEmpty(model.Email) || string.IsNullOrEmpty(model.Token) || string.IsNullOrEmpty(model.NewPassword))
                return BadRequest("Eksik bilgi.");

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null) return BadRequest("Kullanıcı bulunamadı.");

            // Token ve Yeni Şifre ile sıfırlama işlemi
            var result = await _userManager.ResetPasswordAsync(user, model.Token, model.NewPassword);

            if (result.Succeeded)
            {
                // Güvenlik damgasını güncelle (eski oturumları düşürmek için iyi bir pratiktir)
                await _userManager.UpdateSecurityStampAsync(user);
                return Ok(new { message = "Şifreniz başarıyla güncellendi! Giriş yapabilirsiniz." });
            }

            // Hataları döndür (Örn: Token süresi dolmuş, şifre çok basit vs.)
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return BadRequest($"Şifre sıfırlanamadı: {errors}");
        }
    }

    // Register Modeli
    public class RegisterRequest
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string TCKN { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public DateTime BirthDate { get; set; }
    }
    
    // Şifremi Unuttum İsteği
    public class ForgotPasswordRequest
    {
        public string Email { get; set; } = string.Empty;
    }

    // Şifre Sıfırlama İsteği
    public class ResetPasswordRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}