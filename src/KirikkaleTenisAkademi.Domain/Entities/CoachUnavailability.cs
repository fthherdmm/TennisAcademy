using KirikkaleTenisAkademi.Domain.Common;

namespace KirikkaleTenisAkademi.Domain.Entities
{
    // Koçun MÜSAİT OLMADIĞI zamanlar
    public class CoachUnavailability : BaseEntity
    {
        public int CoachId { get; set; }
        public Coach Coach { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string? Reason { get; set; } // Örn: "Dişçi randevusu", "Resmi Tatil"
    }
}