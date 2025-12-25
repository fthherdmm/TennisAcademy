namespace KirikkaleTenisAkademi.Web.Models;

public class MyStudentDto
{
    public string StudentId { get; set; }
    public string FullName { get; set; }
    public string Email { get; set; }
    public string Phone { get; set; }
    public string ProfileImageUrl { get; set; }
    public int CurrentCredits { get; set; }
    public string TCKN { get; set; }
    
    // Fiziksel & Teknik
    public DateTime? BirthDate { get; set; }
    public int Age => BirthDate.HasValue ? DateTime.Now.Year - BirthDate.Value.Year : 0;
    public double? Height { get; set; }
    public double? Weight { get; set; }
    public string Level { get; set; } // Enum string'e çevrilip dönecek
    public string Hand { get; set; }
    
    // Acil Durum
    public string EmergencyContact { get; set; }
}