using System.Security.Claims;
using System.Text.Json;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Headers;

namespace KirikkaleTenisAkademi.Web.Auth
{
    public class CustomAuthStateProvider : AuthenticationStateProvider
    {
        private readonly ILocalStorageService _localStorage;
        private readonly HttpClient _http;

        public CustomAuthStateProvider(ILocalStorageService localStorage, HttpClient http)
        {
            _localStorage = localStorage;
            _http = http;
        }

        // Blazor "Kullanıcı kim?" diye sorduğunda burası çalışır
        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            // 1. Token'ı hafızadan çek
            string token = await _localStorage.GetItemAsStringAsync("authToken");

            var identity = new ClaimsIdentity();
            _http.DefaultRequestHeaders.Authorization = null;

            if (!string.IsNullOrEmpty(token))
            {
                try
                {
                    // 2. Token varsa içini oku (Claims)
                    identity = new ClaimsIdentity(ParseClaimsFromJwt(token), "jwt");
                    // 3. Sonraki istekler için token'ı header'a ekle
                    _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Replace("\"", ""));
                }
                catch
                {
                    // Token bozuksa veya süresi dolduysa sil
                    await _localStorage.RemoveItemAsync("authToken");
                    identity = new ClaimsIdentity();
                }
            }

            var user = new ClaimsPrincipal(identity);
            var state = new AuthenticationState(user);

            // Sisteme haber ver
            NotifyAuthenticationStateChanged(Task.FromResult(state));

            return state;
        }

        // --- YARDIMCI METOT: JWT PARSE İŞLEMİ (Burası biraz teknik, kopyala geç) ---
        public static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
        {
            var payload = jwt.Split('.')[1];
            var jsonBytes = ParseBase64WithoutPadding(payload);
            var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes);
            // 👇 JSON İçindeki Rolleri Düzgün Okuma Mantığı
            var claims = new List<Claim>();
            foreach (var kvp in keyValuePairs)
            {
                switch (kvp.Key)
                {
                    case "role": // Rolleri yakala
                        if (kvp.Value is JsonElement element && element.ValueKind == JsonValueKind.Array)
                        {
                            // Birden fazla rol varsa
                            foreach (var item in element.EnumerateArray())
                            {
                                claims.Add(new Claim(ClaimTypes.Role, item.ToString()));
                            }
                        }
                        else
                        {
                            // Tek rol varsa
                            claims.Add(new Claim(ClaimTypes.Role, kvp.Value.ToString()));
                        }
                        break;
                    default:
                        claims.Add(new Claim(kvp.Key, kvp.Value.ToString()));
                        break;
                }
            }
            return claims;        
        }

        private static byte[] ParseBase64WithoutPadding(string base64)
        {
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }
            return Convert.FromBase64String(base64);
        }
    }
}