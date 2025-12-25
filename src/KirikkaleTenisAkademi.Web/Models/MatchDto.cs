namespace KirikkaleTenisAkademi.Web.Models
{
    public class MatchDto
    {
        public int Id { get; set; }
        
        // Turnuva Seçimi İçin
        public int TournamentId { get; set; } 
        public string TournamentName { get; set; } = string.Empty;

        public DateTime MatchDate { get; set; } = DateTime.Now;

        // Rakip Bilgisi (Manuel giriş şimdilik)
        public string OpponentName { get; set; } = string.Empty; 

        // Set Skorları
        public string ScoreSet1 { get; set; } = "0-0";
        public string? ScoreSet2 { get; set; }
        public string? ScoreSet3 { get; set; }

        // Sonuç (Frontend'de Win/Loss seçtirip, Backend'de WinnerId'ye çevireceğiz)
        public bool IsWinner { get; set; } = true; 

        public string? CoachNotes { get; set; }
        public string StudentId { get; set; } = string.Empty;
    }
    
    // Turnuva listesini dropdown'da göstermek için basit DTO
    public class TournamentSelectDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}