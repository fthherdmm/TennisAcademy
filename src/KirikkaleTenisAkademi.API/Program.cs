using KirikkaleTenisAkademi.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using KirikkaleTenisAkademi.Domain.Entities;
using Microsoft.AspNetCore.Identity;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// 1. AYARLAR VE SERVİSLER
// ==========================================

// CORS Politikası: Her yerden gelen isteğe izin ver (Geliştirme aşaması için)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy =>
        {
            policy.AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
        });
});

// Controller Desteği
builder.Services.AddControllers();

// Swagger Ayarları (JWT Kilit Butonu Dahil)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(option =>
{
    option.SwaggerDoc("v1", new OpenApiInfo { Title = "Kirikkale Tenis Akademi API", Version = "v1" });
    
    option.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Lütfen token'ı 'Bearer {token}' formatında giriniz.",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey
    });
    
    option.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] { }
        }
    });
});

// Altyapı (Infrastructure) Katmanını Ekle
// NOT: AddInfrastructure metodunun içinde AddIdentity<AppUser, IdentityRole> olduğundan emin ol!
builder.Services.AddInfrastructure(builder.Configuration);

// JWT Auth Ayarları
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = Encoding.UTF8.GetBytes(jwtSettings["Secret"]!);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(secretKey)
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

// ==========================================
// 2. MIDDLEWARE KATMANI
// ==========================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AllowAll"); // Frontend (Blazor) erişimi için şart

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ... (Kodun üst kısımları aynı kalsın) ...

// ==========================================
// 3. SEED DATA (OTOMATİK VERİ OLUŞTURMA)
// ==========================================
using (var scope = app.Services.CreateScope())
{
    try 
    {
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        // 1. Rolleri Kontrol Et ve Oluştur
        string[] roles = { "Admin", "Coach", "Student" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
                Console.WriteLine($">>> ROL OLUŞTURULDU: {role}");
            }
        }

        // 2. Admin Kullanıcısı İşlemleri (GARANTİ YÖNTEM)
        var adminEmail = "admin@tenis.com";
        
        // Önce veritabanında bu email ile kayıtlı biri var mı bak
        var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
        
        if (existingAdmin != null)
        {
            // VARSA SİL! (Çünkü şifresi bozuk olabilir veya eski yapıdan kalmış olabilir)
            Console.WriteLine(">>> ESKİ ADMIN KAYDI BULUNDU. TEMİZ KURULUM İÇİN SİLİNİYOR...");
            await userManager.DeleteAsync(existingAdmin);
        }

        // Şimdi TERTEMİZ bir Admin oluştur
        var newAdmin = new AppUser() 
        {
            UserName = "admin", // Login olurken UserName kullanıyorsan buna dikkat!
            Email = adminEmail,
            FirstName = "Sistem", 
            LastName = "Yöneticisi", 
            EmailConfirmed = true,
            LessonCredits = 9999, // Bol kredi
            SecurityStamp = Guid.NewGuid().ToString() // Bu olmazsa Login hata verir!
        };

        // Kullanıcıyı oluştur ve şifresini belirle
        var createResult = await userManager.CreateAsync(newAdmin, "Admin123!"); 

        if (createResult.Succeeded)
        {
            // Rolünü ver
            await userManager.AddToRoleAsync(newAdmin, "Admin");
            Console.WriteLine(">>> ✅ BAŞARILI: Yeni Admin kullanıcısı (AppUser) oluşturuldu.");
            Console.WriteLine($">>> Giriş Bilgileri: Email: {adminEmail} | Şifre: Admin123!");
        }
        else
        {
            Console.WriteLine(">>> ❌ HATA: Admin kullanıcısı oluşturulamadı!");
            foreach (var error in createResult.Errors)
            {
                Console.WriteLine($">>> HATA DETAYI: {error.Description}");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($">>> 💥 KRİTİK SEED DATA HATASI: {ex.Message}");
    }
}

app.Run();