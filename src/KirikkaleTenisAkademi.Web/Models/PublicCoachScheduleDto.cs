namespace KirikkaleTenisAkademi.Web.Models
{
    public class PublicCoachScheduleDto
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Type { get; set; } // "Lesson" veya "Blocked"
    }
}