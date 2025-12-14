using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using KirikkaleTenisAkademi.Application.DTOs.Auth;
using KirikkaleTenisAkademi.Application.Interfaces;
using KirikkaleTenisAkademi.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace KirikkaleTenisAkademi.Infrastructure.Services
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AuthService(UserManager<AppUser> userManager, 
            IConfiguration configuration,
            RoleManager<IdentityRole> roleManager) // <-- Eklendi
        {
            _userManager = userManager;
            _configuration = configuration;
            _roleManager = roleManager; // <-- Eklendi
        }

        public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
        {
            // 1. Yeni Kullanıcı Oluştur
            var user = new AppUser
            {
                UserName = request.UserName,
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName
            };

            // 2. Şifreyi Hashleyerek Kaydet (CreateAsync bu işi yapar)
            var result = await _userManager.CreateAsync(user, request.Password);

            if (!result.Succeeded)
            {
                // Hata varsa (Örn: Şifre çok basit, Email kayıtlı) ilk hatayı fırlat
                throw new Exception(result.Errors.First().Description);
            }

            // 3. Başarılıysa Token üret ve dön
            return await GenerateAuthResponseAsync(user);
        }

        public async Task<AuthResponse> LoginAsync(LoginRequest request)
        {
            // 1. Kullanıcıyı Email ile bul
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null) throw new Exception("Kullanıcı bulunamadı.");

            // 2. Şifreyi Kontrol Et
            var isPasswordValid = await _userManager.CheckPasswordAsync(user, request.Password);
            if (!isPasswordValid) throw new Exception("Şifre hatalı.");

            // 3. Başarılıysa Token üret ve dön
            return await GenerateAuthResponseAsync(user);
        }

        // --- YARDIMCI METOT: TOKEN ÜRETME MAKİNESİ ---
        private async Task<AuthResponse> GenerateAuthResponseAsync(AppUser user)
        {
            // Token'ın içine gizleyeceğimiz bilgiler (Claims)
            var authClaims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.UserName ?? ""),
                new Claim(ClaimTypes.Email, user.Email ?? "")
            };

            // 👇 YENİ: Kullanıcının Rollerini Bul ve Token'a Ekle
            var userRoles = await _userManager.GetRolesAsync(user);
            foreach (var role in userRoles)
            {
                authClaims.Add(new Claim(ClaimTypes.Role, role));
            }
            
            // İmza için gizli anahtarımızı alıyoruz
            var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JwtSettings:Secret"]!));

            // Token'ı oluşturuyoruz
            var token = new JwtSecurityToken(
                issuer: _configuration["JwtSettings:Issuer"],
                audience: _configuration["JwtSettings:Audience"],
                expires: DateTime.Now.AddMinutes(Convert.ToDouble(_configuration["JwtSettings:DurationInMinutes"])),
                claims: authClaims,
                signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
            );

            return new AuthResponse
            {
                Id = user.Id,
                UserName = user.UserName!,
                Email = user.Email!,
                Token = new JwtSecurityTokenHandler().WriteToken(token) // Token'ı string'e çevir
            };
        }
    }
}