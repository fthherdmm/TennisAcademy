namespace KirikkaleTenisAkademi.Application.DTOs
{
    public class GrantCreditRequest
    {
        public string StudentId { get; set; } // Öğrencinin GUID'i
        public int Amount { get; set; } // Kaç kredi? (örn: 1)
    }
}