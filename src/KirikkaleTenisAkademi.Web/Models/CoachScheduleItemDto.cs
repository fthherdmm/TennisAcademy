namespace KirikkaleTenisAkademi.Web.Models
{
    public class CoachScheduleItemDto
    {
        public int Id { get; set; } // Ders ID veya Unavailability ID
        public string StudentId { get; set; } // Yeni Eklendi
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Type { get; set; } // "Lesson" veya "Block"
        public string Title { get; set; } // "Ahmet Yılmaz" veya "Doktor Randevusu"
        public string LessonType { get; set; } = "Private";
    }
}