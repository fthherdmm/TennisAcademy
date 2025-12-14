using KirikkaleTenisAkademi.Domain.Entities;
using KirikkaleTenisAkademi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims; // Bunu eklemeyi unutma
using Microsoft.AspNetCore.Authorization; // Bunu da

namespace KirikkaleTenisAkademi.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CoachesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CoachesController(AppDbContext context)
        {
            _context = context;
        }

        // Tüm koçları getir
        [HttpGet]
        public async Task<ActionResult<List<Coach>>> GetAll()
        {
            return await _context.Coaches.ToListAsync();
        }

        // Geçici: Hızlıca koç eklemek için (Admin paneli yapana kadar)
        [HttpPost]
        public async Task<ActionResult<Coach>> Create(Coach coach)
        {
            _context.Coaches.Add(coach);
            await _context.SaveChangesAsync();
            return Ok(coach);
        }
        
        // 3. GİRİŞ YAPAN HOCANIN BİLGİLERİNİ GETİR
        [HttpGet("current")]
        [Authorize(Roles = "Coach")] // Sadece hocalar kullanabilir
        public async Task<ActionResult<Coach>> GetCurrentCoach()
        {
            // Token'dan User ID'yi (GUID) al
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Veritabanında bu User ID'ye sahip Koçu bul
            var coach = await _context.Coaches
                .FirstOrDefaultAsync(c => c.AppUserId == userId);

            if (coach == null)
                return NotFound("Hoca profili bulunamadı.");

            return Ok(coach);
        }
        
        
    }
}