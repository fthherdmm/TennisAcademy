using KirikkaleTenisAkademi.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using KirikkaleTenisAkademi.API;
using KirikkaleTenisAkademi.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

// Postgresql timestamp sorunu için fix
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// 1. AYARLAR VE SERVİSLER
// ==========================================

// 🔴 DÜZELTME 1: CORS Politikası (Frontend'e Özel)
// "AllowAll" yerine Web projenin adresine özel izin veriyoruz.
// Bu sayede "Preflight Redirect" hatalarını engelleriz.
builder.Services.AddCors(options =>
{
    options.AddPolicy("MySecurePolicy",
        builder =>
        {
            builder
                .WithOrigins(
                    // "https://www.winnertenis.com", 
                    // "https://winnertenis.com"
                    AppConstants.WebBaseUrl
                )
                .AllowAnyMethod()
                .AllowAnyHeader();
        });
});

// Controller Desteği
builder.Services.AddControllers();

// Swagger Ayarları
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

// Altyapı Katmanı
builder.Services.AddInfrastructure(builder.Configuration);

// JWT Auth Ayarları
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
// Eğer secret key boşsa patlamaması için varsayılan bir değer atayalım (Production'da tehlikeli ama dev için ok)
var secretStr = jwtSettings["Secret"] ?? "bu_varsayilan_cok_gizli_bir_anahtardir_en_az_32_karakter"; 
var secretKey = Encoding.UTF8.GetBytes(secretStr);

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

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        // DbContext'i bul (Adı AppDbContext ise)
        // using Infrastructure.Persistence; ve using Microsoft.EntityFrameworkCore; eklemeyi unutma
        var context = services.GetRequiredService<KirikkaleTenisAkademi.Infrastructure.Persistence.AppDbContext>();
        
        // Veritabanı yoksa oluşturur, varsa eksik migrationları yapar
        context.Database.Migrate(); 
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Veritabanı güncellenirken bir hata oluştu (Migration Error).");
    }
}


// ==========================================
// 2. MIDDLEWARE KATMANI (SIRALAMA ÇOK ÖNEMLİ)
// ==========================================

// if (app.Environment.IsDevelopment())
// {
    app.UseSwagger();
    app.UseSwaggerUI();
// }

// 🔴 Loglama Middleware (En başta dursun ki her şeyi görelim)
app.Use(async (context, next) =>
{
    Console.WriteLine($"🌍 GELEN İSTEK: {context.Request.Method} {context.Request.Path}");
    await next();
});

app.UseStaticFiles();

// HTTPS Yönlendirmesi
app.UseHttpsRedirection();

// 🔴 DÜZELTME 2: CORS Middleware'i (Authentication'dan ÖNCE olmalı)
// Yukarıda tanımladığımız "AllowWeb" politikasını kullan.
app.UseCors("MySecurePolicy");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ==========================================
// 3. SEED DATA (OTOMATİK VERİ OLUŞTURMA)
// ==========================================
using (var scope = app.Services.CreateScope())
{
    try 
    {
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        string[] roles = { "Admin", "Coach", "Student" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var adminEmail = "admin@tenis.com";
        var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
        
        if (existingAdmin != null)
        {
            // Mevcut admin varsa ellemiyoruz (Silmek bazen veri kaybı yaratabilir, development için silebilirsin ama productionda riskli)
            // Eğer şifreyi unuttuysan delete satırını açabilirsin.
             // await userManager.DeleteAsync(existingAdmin); 
        }
        else
        {
            var newAdmin = new AppUser() 
            {
                UserName = "admin",
                Email = adminEmail,
                FirstName = "Sistem", 
                LastName = "Yöneticisi", 
                EmailConfirmed = true,
                LessonCredits = 9999,
                SecurityStamp = Guid.NewGuid().ToString()
            };

            var createResult = await userManager.CreateAsync(newAdmin, "Admin123!"); 
            if (createResult.Succeeded)
            {
                await userManager.AddToRoleAsync(newAdmin, "Admin");
                Console.WriteLine(">>> ✅ ADMIN OLUŞTURULDU: admin@tenis.com / Admin123!");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($">>> SEED DATA HATASI: {ex.Message}");
    }
}

app.Run();