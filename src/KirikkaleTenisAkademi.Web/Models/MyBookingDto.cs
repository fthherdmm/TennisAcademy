namespace KirikkaleTenisAkademi.Web.Models
{
    public class MyBookingDto
    {
        public int Id { get; set; }
        public string CoachName { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Status { get; set; } // Confirmed, Cancelled vs.
        public string LessonType { get; set; } = "Private";
    }
}