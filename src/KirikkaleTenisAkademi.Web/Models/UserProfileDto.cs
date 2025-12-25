namespace KirikkaleTenisAkademi.Web.Models // API tarafında da aynısı olmalı
{
    public class UserProfileDto
    {
        // YÖNETİM İÇİN GEREKLİLER (YENİ EKLENDİ)
        public string Id { get; set; } = string.Empty; 
        public List<string> Roles { get; set; } = new();

        // KİŞİSEL
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string FullName => $"{FirstName} {LastName}"; // Kolaylık olsun diye
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string TCKN { get; set; } = string.Empty;
        public DateTime? BirthDate { get; set; }

        // FİZİKSEL & TEKNİK
        public double? Height { get; set; }
        public double? Weight { get; set; }
        public string Level { get; set; } = string.Empty;
        public string DominantHand { get; set; } = string.Empty;
        public string BackhandStyle { get; set; } = string.Empty;

        // ACİL DURUM
        public string EmergencyContactName { get; set; } = string.Empty;
        public string EmergencyContactPhone { get; set; } = string.Empty;
    }
}