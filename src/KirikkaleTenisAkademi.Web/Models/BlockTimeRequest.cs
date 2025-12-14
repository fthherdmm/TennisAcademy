namespace KirikkaleTenisAkademi.Web.Models // Namespace'i .Web.Models yapıyoruz
{
    public class BlockTimeRequest
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string? Reason { get; set; }
    }
}