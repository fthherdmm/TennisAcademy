using KirikkaleTenisAkademi.Domain.Common;
using KirikkaleTenisAkademi.Domain.Enums;

namespace KirikkaleTenisAkademi.Domain.Entities
{
    public class LessonBooking : BaseEntity
    {
        public string StudentId { get; set; } // Identity User Id
        public AppUser Student { get; set; }

        public int CoachId { get; set; }
        public Coach Coach { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        // Müsaitlik durumunu kontrol etmek ve krediyi düşmek için durumlar
        public BookingStatus Status { get; set; } = BookingStatus.Pending; 
        
        // Bu ders için öğrencinin notu veya koçun maç notu olabilir
        public string? Notes { get; set; } 
    }
}