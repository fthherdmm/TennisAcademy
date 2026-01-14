using KirikkaleTenisAkademi.Application.Interfaces;
using KirikkaleTenisAkademi.Domain.Entities;
using KirikkaleTenisAkademi.Infrastructure.Persistence;
using KirikkaleTenisAkademi.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KirikkaleTenisAkademi.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. PostgreSQL Bağlantısı
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        // 2. Identity (Kullanıcı Yönetimi) Bağlantısı
        services.AddIdentity<AppUser, IdentityRole>(options => 
        {
            // Şifre kuralları (Zaten vardır)
            options.Password.RequireDigit = false;
            options.Password.RequiredLength = 6;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireLowercase = false;
                // 🔥 YENİ EKLENEN KISIMLAR:
            options.User.RequireUniqueEmail = true;       // Aynı maille tekrar kayıt olunamasın
            options.SignIn.RequireConfirmedEmail = true;  // 🛑 MAİL ONAYI OLMADAN GİRİŞ YAPILAMAZ!
        })
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders(); // 👈 BU ÇOK ÖNEMLİ! Token üretmek için bunu mutlaka ekle.
        
        // 3. Auth Servisini Kaydet
        services.AddScoped<IAuthService, AuthService>();

        // 4. Ödeme Servisini Kaydet (BURAYA EKLİYORUZ) ✅
        services.AddScoped<IPaymentService, PaymentService>();

        services.AddScoped<IEmailService, SmtpEmailService>();
        return services;
    }
}