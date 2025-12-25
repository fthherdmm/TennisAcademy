using System.ComponentModel.DataAnnotations.Schema;
using KirikkaleTenisAkademi.Domain.Common;

namespace KirikkaleTenisAkademi.Domain.Entities;

public class Match : BaseEntity
{
    public int TournamentId { get; set; }
    public Tournament Tournament { get; set; }

    // 1. Oyuncu (Bizim öğrencimiz)
    public string Player1Id { get; set; }
    [ForeignKey("Player1Id")]
    public AppUser Player1 { get; set; }

    // 2. Oyuncu (Bizim öğrencimiz OLABİLİR veya DIŞARIDAN İSİM olabilir)
    // Eğer rakip bizim akademiden değilse ID null olur, sadece ismini yazarız.
    public string? Player2Id { get; set; } 
    [ForeignKey("Player2Id")]
    public AppUser? Player2 { get; set; }

    public string Player2ExternalName { get; set; } = string.Empty; // Eğer Player2Id yoksa buraya "Ahmet Mehmet" yazarız.

    // Skor Detayları (Örn: 6-4, 6-2)
    public string ScoreSet1 { get; set; } = "0-0";
    public string? ScoreSet2 { get; set; }
    public string? ScoreSet3 { get; set; }

    public string? WinnerId { get; set; } // Kazananın ID'si (Bizim öğrencimizse)
        
    public DateTime MatchDate { get; set; }
    
    // Maçı sisteme giren Koç (Loglamak için önemli)
    public int RefereeCoachId { get; set; } 
    
    // Maç hakkında notlar (Örn: "Forehand tarafına çok baskı yedi.")
    public string? CoachNotes { get; set; } 
}