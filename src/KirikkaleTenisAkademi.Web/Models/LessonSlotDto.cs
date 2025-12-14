namespace KirikkaleTenisAkademi.Web.Models
{
    public class LessonSlotDto
    {
        public int Id { get; set; }
        public int CoachId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool IsBooked { get; set; }
        public decimal Price { get; set; }
        public string? CoachName { get; set; }
        public string? CoachImage { get; set; }
    }
}