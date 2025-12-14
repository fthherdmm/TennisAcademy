using KirikkaleTenisAkademi.Domain.Entities; // Eğer Match ve LessonBooking buradaysa
using Microsoft.AspNetCore.Identity;

namespace KirikkaleTenisAkademi.Domain.Entities
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
        
        // YENİ EKLENMESİ GEREKEN ALAN:
        public int LessonCredits { get; set; } = 0; 
        
        // Profil resmi (İsteğe bağlı, şimdilik dursun)
        public string? ProfileImageUrl { get; set; }

        // Navigation Properties (İlişkiler) - Controller'da Include kullanmıştık
        public ICollection<LessonBooking>? Bookings { get; set; }
    }
}