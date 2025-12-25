namespace KirikkaleTenisAkademi.Web.Models;

public class CreateTournamentDto
{
    public string Name { get; set; } = string.Empty; // Örn: 19 Mayıs Kupası
    public string Category { get; set; } = string.Empty; // Örn: 14 Yaş
    public DateTime StartDate { get; set; } = DateTime.Now;
    public DateTime EndDate { get; set; } = DateTime.Now.AddDays(7);
}