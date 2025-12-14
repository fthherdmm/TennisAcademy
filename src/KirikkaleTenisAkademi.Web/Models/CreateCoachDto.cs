using System.ComponentModel.DataAnnotations;

namespace KirikkaleTenisAkademi.Web.Models
{
    public class CreateCoachDto
    {
        [Required(ErrorMessage = "E-posta zorunludur.")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta giriniz.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Şifre zorunludur.")]
        [MinLength(6, ErrorMessage = "Şifre en az 6 karakter olmalı.")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ad zorunludur.")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Soyad zorunludur.")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Uzmanlık alanı zorunludur.")]
        public string Specialty { get; set; } = string.Empty; // Örn: Çocuk Tenisi

        public string Bio { get; set; } = string.Empty;

        // Varsayılan bir resim atayalım, admin isterse değiştirir
        public string ImageUrl { get; set; } = "https://mudblazor.com/images/users/avatars/1.jpg";
    }
}