namespace KirikkaleTenisAkademi.Web.Models;

public class CoachManualBookingDto
{
    public string StudentId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Type { get; set; } // "Private" veya "Group"
    public string? Title { get; set; } // Opsiyonel başlık
}