namespace KirikkaleTenisAkademi.Web.Models
{
    public class UpdateStudentStatsDto
    {
        public string StudentId { get; set; } = string.Empty;
        
        // Sadece güncellenecek alanlar
        public double? Height { get; set; }
        public double? Weight { get; set; }
        public DateTime? BirthDate { get; set; }
        public string Level { get; set; } = string.Empty;
        public string Hand { get; set; } = string.Empty;
    }
}