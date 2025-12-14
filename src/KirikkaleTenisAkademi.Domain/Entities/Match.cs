using KirikkaleTenisAkademi.Domain.Common;

namespace KirikkaleTenisAkademi.Domain.Entities;

public class Match : BaseEntity
{
    public int TournamentId { get; set; }
    public Tournament Tournament { get; set; }

    // 1. Oyuncu (Bizim öğrencimiz olabilir)
    public string Player1Id { get; set; }
    public AppUser Player1 { get; set; }

    // 2. Oyuncu (Bizim öğrencimiz veya dışarıdan biri olabilir - şimdilik sistem içi yapalım)
    public string Player2Id { get; set; }
    public AppUser Player2 { get; set; }

    // Skorlar
    public int Player1Score { get; set; } // Set sayısı veya oyun sayısı
    public int Player2Score { get; set; }

    public string? WinnerId { get; set; } // Kazananın ID'si
        
    public DateTime MatchDate { get; set; }
        
    // Maçı sisteme giren Koç
    public int RefereeCoachId { get; set; } 
}