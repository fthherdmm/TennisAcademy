namespace KirikkaleTenisAkademi.Web.Models
{
    public class MyStudentDto
    {
        public string StudentId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public int CurrentCredits { get; set; } // Öğrencinin güncel bakiyesini de görelim
    }
}