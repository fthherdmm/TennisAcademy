using System.ComponentModel.DataAnnotations;

namespace KirikkaleTenisAkademi.Web.Models
{
    public class CreateSlotDto
    {
        [Required]
        public DateTime? Date { get; set; } = DateTime.Today; // Hangi Gün?

        [Required]
        public TimeSpan? StartTime { get; set; } = new TimeSpan(09, 00, 00); // Saat Kaçta?

        [Required]
        public TimeSpan? EndTime { get; set; } = new TimeSpan(10, 00, 00); // Kaçta Bitiyor?

        [Required]
        [Range(1, 5000, ErrorMessage = "Fiyat 1 ile 5000 arasında olmalı.")]
        public decimal Price { get; set; } = 500; // Kaç Para?
    }
}