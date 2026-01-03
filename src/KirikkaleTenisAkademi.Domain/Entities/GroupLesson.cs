using KirikkaleTenisAkademi.Domain.Common;
using KirikkaleTenisAkademi.Domain.Enums;

namespace KirikkaleTenisAkademi.Domain.Entities
{
    public class GroupLesson : BaseEntity // Id, CreatedDate vb. BaseEntity'den gelir
    {
        public string Title { get; set; } = string.Empty; // Örn: "Yetişkin Başlangıç Grubu"
        public string? Description { get; set; } // Örn: "Forehand teknikleri çalışılacak."
        
        public int CoachId { get; set; }
        public Coach Coach { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        public int Capacity { get; set; } = 4; // Kontenjan (Örn: Maks 4 kişi)
        public int RegisteredCount { get; set; } = 0; // Şu an kaç kişi var

        public TennisLevel MinLevel { get; set; } = TennisLevel.Beginner; // Bu derse kimler gelebilir?
        
        public int CreditCost { get; set; } = 1; // Bu ders kaç "Grup Kredisi" harcar?

        public bool IsActive { get; set; } = true; // İptal edildi mi?

        // Kayıt olan öğrenciler
        public ICollection<GroupLessonRegistration> Registrations { get; set; }
    }
}