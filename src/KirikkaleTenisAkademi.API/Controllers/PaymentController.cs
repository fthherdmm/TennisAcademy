using KirikkaleTenisAkademi.Domain.Entities;
using KirikkaleTenisAkademi.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Iyzipay.Model;
using KirikkaleTenisAkademi.Infrastructure.Persistence; 
using Microsoft.AspNetCore.Http;

namespace KirikkaleTenisAkademi.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly UserManager<AppUser> _userManager;
        private readonly AppDbContext _context;

        public PaymentController(
            IPaymentService paymentService, 
            UserManager<AppUser> userManager, 
            AppDbContext context)
        {
            _context = context;
            _paymentService = paymentService;
            _userManager = userManager;
        }

        [HttpPost("start/{packageId}")]
        public async Task<IActionResult> StartPayment(int packageId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized("Kullanıcı bulunamadı.");

            var packet = await _context.LessonPackets.FindAsync(packageId);

            if (packet == null) return NotFound("Böyle bir paket yok.");
            if (!packet.IsActive) return BadRequest("Bu paket şu an satışta değil.");

            decimal price = packet.Price;
            int creditAmount = packet.CreditAmount;

            string conversationId = $"{user.Id}|ORDER_{DateTime.Now.Ticks}|{creditAmount}";

            var form = await _paymentService.GetPaymentForm(user, price, creditAmount, conversationId);

            if (form.Status != "success")
            {
                return BadRequest($"Iyzico Hatası: {form.ErrorMessage}");
            }

            Console.WriteLine("🚀 CALLBACK TETİKLENDİ...");
            
            return Ok(new { 
                token = form.Token, 
                paymentPageUrl = form.PaymentPageUrl 
            });
        }

        [HttpPost("callback")]
        [AllowAnonymous]
        [IgnoreAntiforgeryToken]
        [Consumes("application/x-www-form-urlencoded")]
        // 🔴 DEĞİŞİKLİK 1: Parametreye 'backupId' eklendi
        public async Task<IActionResult> Callback(IFormCollection form, [FromQuery] string? backupId)
        {
            string debugMessage = "";
            string webBaseUrl = "https://localhost:7207"; 

            try
            {
                string token = form["token"];
                if (string.IsNullOrEmpty(token)) 
                    return Content("<h1>HATA: Token Iyzico'dan gelmedi.</h1>", "text/html");

                var result = await _paymentService.RetrievePaymentResult(token);

                if (result.Status == "success" && result.PaymentStatus == "SUCCESS")
                {
                    // 🔴 DEĞİŞİKLİK 2: Veriyi Garantiye Alıyoruz
                    // Önce Iyzico'dan gelene bak, yoksa URL'den (backupId) al.
                    string finalConversationId = result.ConversationId;

                    if (string.IsNullOrEmpty(finalConversationId))
                    {
                        finalConversationId = backupId; // URL'deki yedeği kullan
                        debugMessage += "⚠️ Uyarı: Iyzico ID boş döndü, Yedek (URL) ID kullanıldı.<br/>";
                    }

                    if (!string.IsNullOrEmpty(finalConversationId))
                    {
                        debugMessage += $"İşlenen ID: {finalConversationId} <br/>";
                        
                        var parts = finalConversationId.Split('|');
                        
                        if (parts.Length >= 3)
                        {
                            var userId = parts[0];
                            var creditsString = parts[2];

                            if (int.TryParse(creditsString, out int creditsToAdd))
                            {
                                var user = await _userManager.FindByIdAsync(userId);
                                
                                if (user != null)
                                {
                                    int oldCredits = user.LessonCredits;
                                    user.LessonCredits += creditsToAdd;

                                    var updateResult = await _userManager.UpdateAsync(user);

                                    if (updateResult.Succeeded)
                                    {
                                        // ✅ BAŞARILI
                                        string successHtml = $@"
                                            <html>
                                            <head><title>Ödeme Başarılı</title></head>
                                            <body style='text-align:center; padding-top:50px; font-family:sans-serif;'>
                                                <h1 style='color:green;'>✅ Kredi Yüklendi!</h1>
                                                <p><b>{user.Email}</b> hesabına {creditsToAdd} kredi eklendi.</p>
                                                <p>Eski: {oldCredits} -> Yeni: {user.LessonCredits}</p>
                                                <script>
                                                    setTimeout(function() {{
                                                        window.location.href = '{webBaseUrl}/payment-success';
                                                    }}, 3000);
                                                </script>
                                            </body>
                                            </html>";
                                        return Content(successHtml, "text/html");
                                    }
                                    else
                                    {
                                        debugMessage += "❌ Veritabanı Hatası: ";
                                        foreach (var err in updateResult.Errors) debugMessage += err.Description + " ";
                                    }
                                }
                                else
                                {
                                    debugMessage += $"❌ Kullanıcı Bulunamadı! ID: {userId}";
                                }
                            }
                            else
                            {
                                debugMessage += $"❌ Kredi miktarı sayı değil: {creditsString}";
                            }
                        }
                        else
                        {
                            debugMessage += $"❌ ID Formatı Hatalı. Gelen: {finalConversationId}";
                        }
                    }
                    else
                    {
                        debugMessage += "❌ HATA: ID hem Iyzico'da hem URL'de yok!";
                    }
                }
                else
                {
                    debugMessage += $"❌ Ödeme Başarısız: {result.ErrorMessage}";
                }
            }
            catch (Exception ex)
            {
                debugMessage += $"💥 SİSTEM HATASI: {ex.Message}";
            }

            // HATA EKRANI
            string errorHtml = $@"
                <html>
                <body style='text-align:center; padding-top:20px; font-family:sans-serif;'>
                    <h1 style='color:red;'>⚠️ İşlem Tamamlanamadı</h1>
                    <div style='background-color:#ffebee; padding:20px; border:1px solid red; display:inline-block;'>
                        <p>{debugMessage}</p>
                    </div>
                    <br/><br/><a href='{webBaseUrl}'>Ana Sayfaya Dön</a>
                </body>
                </html>";
                
            return Content(errorHtml, "text/html");
        }
    }
}