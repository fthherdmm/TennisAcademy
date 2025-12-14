namespace KirikkaleTenisAkademi.Web.Models
{
    public class CoachScheduleDto
    {
        public int Id { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public decimal Price { get; set; }
        public bool IsBooked { get; set; }
        public string? StudentName { get; set; } // <-- İşte aradığımız şey!
    }
}