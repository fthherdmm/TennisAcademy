using KirikkaleTenisAkademi.Domain.Common;

namespace KirikkaleTenisAkademi.Domain.Entities
{
    public class GroupLessonRegistration : BaseEntity
    {
        public int GroupLessonId { get; set; }
        public GroupLesson GroupLesson { get; set; }

        public string StudentId { get; set; }
        public AppUser Student { get; set; }

        public DateTime RegistrationDate { get; set; } = DateTime.UtcNow;
        
        public bool IsAttended { get; set; } = false; // Derse geldi mi? (Yoklama için)
    }
}