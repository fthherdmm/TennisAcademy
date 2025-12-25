using KirikkaleTenisAkademi.Domain.Common;

namespace KirikkaleTenisAkademi.Domain.Entities;

public class Tournament : BaseEntity
{
    public string Name { get; set; } // Örn: "Cumhuriyet Kupası 2025"
    public string Category { get; set; } // Örn: "14 Yaş Erkekler", "Senior Mix"
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; }
    
    public ICollection<Match> Matches { get; set; }
}