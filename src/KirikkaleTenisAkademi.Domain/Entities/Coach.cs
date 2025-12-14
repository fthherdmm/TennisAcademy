using KirikkaleTenisAkademi.Domain.Common;

namespace KirikkaleTenisAkademi.Domain.Entities
{
    public class Coach : BaseEntity // BaseEntity'den Id ve CreatedDate gelir
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        
        // Hangi branşta uzman? (Örn: Çocuk Tenisi, Performans, Yetişkin)
        public string Specialty { get; set; } = string.Empty;
        
        // Hakkında yazısı
        public string Bio { get; set; } = string.Empty;
        
        // Profil fotoğrafı URL'i (Örn: "images/ahmet-hoca.jpg")
        public string ImageUrl { get; set; } = "https://mudblazor.com/images/users/avatars/1.jpg"; // Varsayılan resim

        // İlişkiler: Her Koç aslında sistemde bir Kullanıcıdır
        public string AppUserId { get; set; }
        // (Burada navigation property'yi şimdilik nullable yapıyoruz hata almamak için)
        public AppUser? AppUser { get; set; }
    }
}