using KirikkaleTenisAkademi.Domain.Common;

namespace KirikkaleTenisAkademi.Domain.Entities
{
    // Satışa sunulan paketler (Örn: "Tek Ders", "10'lu Avantaj Paketi")
    public class LessonPacket : BaseEntity
    {
        public string Name { get; set; } = string.Empty; // Paket Adı
        public string Description { get; set; } = string.Empty; 
        public decimal Price { get; set; } // Ücret
        public int CreditAmount { get; set; } // Bu paket kaç ders hakkı veriyor? (1, 5, 10 vb.)
        public bool IsActive { get; set; } = true; // Kampanya bittiyse false yaparız
    }
}