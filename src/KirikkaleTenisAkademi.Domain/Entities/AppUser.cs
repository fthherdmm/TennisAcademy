using Microsoft.AspNetCore.Identity;
using KirikkaleTenisAkademi.Domain.Enums;

namespace KirikkaleTenisAkademi.Domain.Entities
{
    public class AppUser : IdentityUser
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        
        public string? TCKN { get; set; }
        public DateTime? BirthDate { get; set; }

        public double? Height { get; set; }
        public double? Weight { get; set; }

        public TennisLevel Level { get; set; } = TennisLevel.Beginner;
        public DominantHand DominantHand { get; set; } = DominantHand.Right;
        public BackhandStyle BackhandStyle { get; set; } = BackhandStyle.TwoHanded;
        
        public string? EmergencyContactName { get; set; }
        public string? EmergencyContactPhone { get; set; }
        public string? MedicalNotes { get; set; }

        public string? ProfileImageUrl { get; set; }
        public int LessonCredits { get; set; } = 0;

        // YENİ EKLENEN: Grup Dersleri için ayrı kredi bakiyesi
        public int GroupCredits { get; set; } = 0;

        // Navigation Property (Yeni tabloyla ilişki)
        public ICollection<GroupLessonRegistration> GroupRegistrations { get; set; }
        
        public ICollection<LessonBooking> Bookings { get; set; }
        public ICollection<Match> MatchesAsPlayer1 { get; set; }
        public ICollection<Match> MatchesAsPlayer2 { get; set; }
        
        public int Age => BirthDate.HasValue ? DateTime.Now.Year - BirthDate.Value.Year : 0;

        // DÜZELTME BURADA: Field yerine Property yaptık.
        public DateTime RegistrationDate { get; set; } = DateTime.UtcNow;
    }
}