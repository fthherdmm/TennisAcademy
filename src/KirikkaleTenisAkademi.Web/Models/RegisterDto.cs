using System.ComponentModel.DataAnnotations;

namespace KirikkaleTenisAkademi.Web.Models
{
    public class RegisterDto
    {
        [Required(ErrorMessage = "Ad Soyad zorunludur.")]
        public string FirstName { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Ad Soyad zorunludur.")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Kullanıcı adı zorunludur.")]
        public string UserName { get; set; } = string.Empty;

        [Required(ErrorMessage = "E-posta zorunludur.")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Şifre zorunludur.")]
        [MinLength(6, ErrorMessage = "Şifre en az 6 karakter olmalı.")]
        // Identity kuralları gereği şifre biraz karmaşık olmalı
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Şifre tekrarı zorunludur.")]
        [Compare(nameof(Password), ErrorMessage = "Şifreler uyuşmuyor.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}