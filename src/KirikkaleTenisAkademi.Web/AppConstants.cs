namespace KirikkaleTenisAkademi.Web // Senin Client projenin namespace'i neyse onu yaz
{
    public static class AppConstants
    {
#if DEBUG
        public const string ApiBaseUrl = "https://localhost:7111"; // ÖRNEKTİR: Kendi Frontend portunu yaz
#else
        public const string ApiBaseUrl = "https://api.winnertenis.com";
#endif
    }
}