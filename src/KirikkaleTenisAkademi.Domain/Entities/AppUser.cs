using Microsoft.AspNetCore.Identity;

namespace KirikkaleTenisAkademi.Domain.Entities
{
    public class AppUser : IdentityUser
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        
        // YENİ: Profil Fotoğrafı
        public string? ProfileImageUrl { get; set; }

        // YENİ: Ders Hakkı Bakiyesi (Örn: 5 ders hakkı kaldı)
        public int LessonCredits { get; set; } = 0;

        // YENİ: Detaylı Profil Bilgileri (İleride property olarak ekleriz veya ayrı tablo yaparız)
        // Şimdilik basit tutuyorum, detayları verdiğinde buraya ekleriz.
        public string? PhysicalStats { get; set; } // Boy, Kilo vs. JSON tutulabilir şimdilik.
        
        // Navigation Properties
        public ICollection<LessonBooking> Bookings { get; set; }
        public ICollection<Match> MatchesPlayed { get; set; }
    }
}