namespace KirikkaleTenisAkademi.API // Senin Client projenin namespace'i neyse onu yaz
{
    public static class AppConstants
    {
#if DEBUG
        public const string WebBaseUrl = "https://localhost:7207"; // ÖRNEKTİR: Kendi Frontend portunu yaz
#else
        public const string WebBaseUrl = "https://www.winnertenis.com/";
#endif
    }
}