using KirikkaleTenisAkademi.Domain.Common;
using KirikkaleTenisAkademi.Domain.Enums;

namespace KirikkaleTenisAkademi.Domain.Entities
{
    public class Packet : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int CreditAmount { get; set; } // Kaç kredi verecek?
        
        // YENİ EKLENEN: Bu paket ne kredisi veriyor?
        public LessonType Type { get; set; } = LessonType.Private; // Varsayılan Bireysel
        
        public bool IsActive { get; set; } = true;
        public string? Description { get; set; }
    }
}