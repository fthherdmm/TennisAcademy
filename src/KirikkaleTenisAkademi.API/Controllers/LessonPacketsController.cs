using System.Security.Claims;
using KirikkaleTenisAkademi.Domain.Entities;
using KirikkaleTenisAkademi.Domain.Enums;
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
        
        [HttpPost]
        [Authorize(Roles = "Admin")] // Sadece Admin ekleyebilir
        public async Task<ActionResult<LessonPacket>> CreatePacket(LessonPacket packet)
        {
            _context.LessonPackets.Add(packet);
            await _context.SaveChangesAsync();

            return Ok(packet);
        }
        
        // PUT: api/LessonPackets/5/toggle-status
        [HttpPut("{id}/toggle-status")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var packet = await _context.LessonPackets.FindAsync(id);
            if (packet == null) return NotFound("Paket bulunamadı.");

            // Durumu tersine çevir (Aktifse Pasif, Pasifse Aktif yap)
            packet.IsActive = !packet.IsActive;
    
            await _context.SaveChangesAsync();
            return Ok(packet); // Güncel hali dön
        }

        // DELETE: api/LessonPackets/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeletePacket(int id)
        {
            var packet = await _context.LessonPackets.FindAsync(id);
            if (packet == null) return NotFound("Paket bulunamadı.");

            _context.LessonPackets.Remove(packet);
            await _context.SaveChangesAsync();

            return NoContent(); // 204 Başarılı ve içerik yok
        }

        // 2. Paket Satın Al (Kredi Yükle)
        [HttpPost("purchase/{packetId}")]
        [Authorize(Roles = "Student")] 
        public async Task<IActionResult> PurchasePacket(int packetId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            // AppUser kullandığımız için _context.Users yerine _userManager veya cast işlemi gerekebilir
            // En temizi AppUser cast etmektir:
            var user = await _context.Users.OfType<AppUser>().FirstOrDefaultAsync(u => u.Id == userId);
        
            var packet = await _context.LessonPackets.FindAsync(packetId);

            if (user == null || packet == null) return NotFound("Kullanıcı veya paket bulunamadı.");
            if (!packet.IsActive) return BadRequest("Bu paket artık satışta değil.");

            // ⚠️ DÜZELTME: Paket tipine göre doğru bakiyeyi artır
            if (packet.Type == LessonType.Group)
            {
                user.GroupCredits += packet.CreditAmount;
            }
            else
            {
                user.LessonCredits += packet.CreditAmount;
            }
        
            await _context.SaveChangesAsync();

            return Ok(new 
            { 
                message = $"{packet.Name} başarıyla alındı.", 
                newLessonBalance = user.LessonCredits,
                newGroupBalance = user.GroupCredits
            });
        }
    }
}