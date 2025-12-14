using System.Security.Claims;
using KirikkaleTenisAkademi.Domain.Entities;
using KirikkaleTenisAkademi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KirikkaleTenisAkademi.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LessonPacketsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public LessonPacketsController(AppDbContext context)
        {
            _context = context;
        }

        // 1. Paketleri Listele (Herkes görebilir)
        [HttpGet]
        public async Task<ActionResult<IEnumerable<LessonPacket>>> GetPackets()
        {
            return await _context.LessonPackets
                .Where(p => p.IsActive)
                .ToListAsync();
        }

        // 2. Paket Satın Al (Kredi Yükle)
        [HttpPost("purchase/{packetId}")]
        [Authorize(Roles = "Student")] // Sadece öğrenciler
        public async Task<IActionResult> PurchasePacket(int packetId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _context.Users.FindAsync(userId);
            var packet = await _context.LessonPackets.FindAsync(packetId);

            if (user == null || packet == null) return NotFound("Kullanıcı veya paket bulunamadı.");
            if (!packet.IsActive) return BadRequest("Bu paket artık satışta değil.");

            // --- BURADA ÖDEME İŞLEMİ SİMÜLE EDİLİR ---
            // Eğer gerçek bir ödeme sistemi (Iyzico, Stripe vb.) olsaydı burada çalışacaktı.
            // Şimdilik ödeme başarılı sayıyoruz ve krediyi ekliyoruz.

            user.LessonCredits += packet.CreditAmount; // Krediyi ekle
            
            await _context.SaveChangesAsync();

            return Ok(new 
            { 
                message = $"{packet.Name} başarıyla alındı.", 
                newBalance = user.LessonCredits 
            });
        }

        // 3. Test İçin Rastgele Paketler Oluştur (Admin veya Herkes - Test bitince sileriz)
        [HttpPost("create-dummy")]
        public async Task<IActionResult> CreateDummyPackets()
        {
            if (_context.LessonPackets.Any()) 
                return BadRequest("Zaten paketler var.");

            var packets = new List<LessonPacket>
            {
                new LessonPacket { Name = "Tek Ders", Description = "Deneme amaçlı tek ders.", Price = 750, CreditAmount = 1, IsActive = true },
                new LessonPacket { Name = "5'li Paket", Description = "%10 İndirimli 5 ders.", Price = 3375, CreditAmount = 5, IsActive = true },
                new LessonPacket { Name = "10'lu Pro Paket", Description = "Profesyonel gelişim için.", Price = 6000, CreditAmount = 10, IsActive = true }
            };

            _context.LessonPackets.AddRange(packets);
            await _context.SaveChangesAsync();

            return Ok("Örnek paketler oluşturuldu.");
        }
    }
}