namespace KirikkaleTenisAkademi.Web.Models
{
    public class MyStudentDto
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string StudentEmail { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }
}