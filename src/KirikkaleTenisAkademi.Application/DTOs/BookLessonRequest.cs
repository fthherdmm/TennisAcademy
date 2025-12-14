// Namespace artık API değil, Application oldu
namespace KirikkaleTenisAkademi.Application.DTOs
{
    public class BookLessonRequest
    {
        public int CoachId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }
}