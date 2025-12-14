using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using KirikkaleTenisAkademi.Web;
using MudBlazor.Services;
using Blazored.LocalStorage; // En tepeye ekle
using Microsoft.AspNetCore.Components.Authorization; // En tepeye ekle
using KirikkaleTenisAkademi.Web.Auth; // Birazdan oluşturacağız

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// "https://localhost:7123" yerine SENİN API PORTUNU yaz.
// API terminalindeki adresi kopyala. Sonunda "/" olmasın.
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("http://localhost:5152") });

// 1. LocalStorage Servisi
builder.Services.AddBlazoredLocalStorage();

// 2. Yetkilendirme Çekirdeği
builder.Services.AddAuthorizationCore();

// 3. Bizim Yazacağımız Özel Auth Provider (Birazdan yazacağız)
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();

builder.Services.AddMudServices();

await builder.Build().RunAsync();
