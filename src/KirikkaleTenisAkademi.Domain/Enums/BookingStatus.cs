namespace KirikkaleTenisAkademi.Domain.Enums
{
    public enum BookingStatus
    {
        Pending = 0,    // Öğrenci talep etti, Hoca onayı bekliyor
        Confirmed = 1,  // Hoca onayladı, ders kesinleşti (Kredi düştü)
        Rejected = 2,   // Hoca reddetti (Kredi iade edildi)
        Completed = 3,  // Ders yapıldı
        Cancelled = 4   // İptal edildi
    }
}