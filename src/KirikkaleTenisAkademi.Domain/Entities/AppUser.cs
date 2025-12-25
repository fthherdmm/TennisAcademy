using Microsoft.AspNetCore.Identity;
using KirikkaleTenisAkademi.Domain.Enums; // Enumları eklemeyi unutma

namespace KirikkaleTenisAkademi.Domain.Entities
{
    public class AppUser : IdentityUser
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        
        // Kimlik ve İletişim
        public string? TCKN { get; set; } // Resmi işlemler ve sigorta için
        // PhoneNumber, IdentityUser'dan geliyor zaten.
        public DateTime? BirthDate { get; set; } // Yaş kategorisi hesaplamak için (Örn: 12 Yaş altı)

        // Fiziksel Özellikler (Takip için)
        public double? Height { get; set; } // cm cinsinden (Örn: 175)
        public double? Weight { get; set; } // kg cinsinden (Örn: 70.5)

        // Tenis Teknik Bilgileri
        public TennisLevel Level { get; set; } = TennisLevel.Beginner;
        public DominantHand DominantHand { get; set; } = DominantHand.Right;
        public BackhandStyle BackhandStyle { get; set; } = BackhandStyle.TwoHanded;
        
        // Ekstra Bilgiler
        public string? EmergencyContactName { get; set; } // Acil Durum Kişisi
        public string? EmergencyContactPhone { get; set; } // Acil Durum No
        public string? MedicalNotes { get; set; } // Kronik rahatsızlık, alerji vb.

        // Sistem İçi Bilgiler
        public string? ProfileImageUrl { get; set; }
        public int LessonCredits { get; set; } = 0;

        // Navigation Properties
        public ICollection<LessonBooking> Bookings { get; set; }
        public ICollection<Match> MatchesAsPlayer1 { get; set; }
        public ICollection<Match> MatchesAsPlayer2 { get; set; }
        
        // Helper Property (Veritabanına kaydedilmez, yaş hesaplar)
        public int Age => BirthDate.HasValue ? DateTime.Now.Year - BirthDate.Value.Year : 0;

        public DateTime RegistrationDate = DateTime.UtcNow;
    }
}