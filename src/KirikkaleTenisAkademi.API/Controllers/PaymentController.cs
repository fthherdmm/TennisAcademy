using KirikkaleTenisAkademi.Domain.Entities;
using KirikkaleTenisAkademi.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Iyzipay.Model;
using KirikkaleTenisAkademi.Infrastructure.Persistence; 
using Microsoft.AspNetCore.Http;
using KirikkaleTenisAkademi.Domain.Enums; // Enum için gerekli

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
            int packetType = (int)packet.Type; // 1: Private, 2: Group

            // 🔴 DEĞİŞİKLİK: ID formatına Paket Tipini ekledik (Sona)
            // Format: UserId | UniqueID | CreditAmount | PacketType
            string conversationId = $"{user.Id}|ORDER_{DateTime.Now.Ticks}|{creditAmount}|{packetType}";

            // Service'e paket adını da gönderiyoruz ki Iyzico ekranında güzel görünsün
            var form = await _paymentService.GetPaymentForm(user, price, creditAmount, conversationId, packet.Name);

            if (form.Status != "success")
            {
                return BadRequest($"Iyzico Hatası: {form.ErrorMessage}");
            }

            return Ok(new { 
                token = form.Token, 
                paymentPageUrl = form.PaymentPageUrl 
            });
        }

        [HttpPost("callback")]
        [AllowAnonymous]
        [IgnoreAntiforgeryToken]
        [Consumes("application/x-www-form-urlencoded")]
        public async Task<IActionResult> Callback(IFormCollection form, [FromQuery] string? backupId)
        {
            string debugMessage = "";
            string webBaseUrl = AppConstants.WebBaseUrl; // Frontend URL'in

            try
            {
                string token = form["token"];
                if (string.IsNullOrEmpty(token)) 
                    return Content("<h1>HATA: Token Iyzico'dan gelmedi.</h1>", "text/html");

                var result = await _paymentService.RetrievePaymentResult(token);

                if (result.Status == "success" && result.PaymentStatus == "SUCCESS")
                {
                    string finalConversationId = result.ConversationId;

                    if (string.IsNullOrEmpty(finalConversationId))
                    {
                        finalConversationId = backupId; 
                        debugMessage += "⚠️ Uyarı: Iyzico ID boş döndü, Yedek (URL) ID kullanıldı.<br/>";
                    }

                    if (!string.IsNullOrEmpty(finalConversationId))
                    {
                        debugMessage += $"İşlenen ID: {finalConversationId} <br/>";
                        
                        var parts = finalConversationId.Split('|');
                        
                        // 🔴 DEĞİŞİKLİK: Artık 4 parça bekliyoruz (Type eklendi)
                        if (parts.Length >= 4)
                        {
                            var userId = parts[0];
                            var creditsString = parts[2];
                            var typeString = parts[3]; // Paket Tipi (1 veya 2)

                            if (int.TryParse(creditsString, out int creditsToAdd) && int.TryParse(typeString, out int typeId))
                            {
                                var user = await _userManager.FindByIdAsync(userId);
                                
                                if (user != null)
                                {
                                    // 🔴 DEĞİŞİKLİK: Krediyi doğru yere yükle
                                    string creditTypeMsg = "";
                                    int oldCredits = 0;
                                    int newCredits = 0;

                                    if (typeId == (int)LessonType.Group)
                                    {
                                        // GRUP KREDİSİ
                                        oldCredits = user.GroupCredits;
                                        user.GroupCredits += creditsToAdd;
                                        newCredits = user.GroupCredits;
                                        creditTypeMsg = "GRUP Dersi";
                                    }
                                    else
                                    {
                                        // BİREYSEL KREDİ (Varsayılan)
                                        oldCredits = user.LessonCredits;
                                        user.LessonCredits += creditsToAdd;
                                        newCredits = user.LessonCredits;
                                        creditTypeMsg = "ÖZEL Ders";
                                    }

                                    var updateResult = await _userManager.UpdateAsync(user);

                                    if (updateResult.Succeeded)
                                    {
                                        string successHtml = $@"
                                            <html>
                                            <head><title>Ödeme Başarılı</title></head>
                                            <body style='text-align:center; padding-top:50px; font-family:sans-serif;'>
                                                <h1 style='color:green;'>✅ Ödeme Başarılı!</h1>
                                                <p><b>{user.Email}</b> hesabına {creditsToAdd} adet <b>{creditTypeMsg}</b> kredisi yüklendi.</p>
                                                <p>Eski: {oldCredits} -> Yeni: {newCredits}</p>
                                                <script>
                                                    setTimeout(function() {{
                                                        window.location.href = '{webBaseUrl}/payment-success';
                                                    }}, 4000);
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
                                debugMessage += "❌ ID Ayrıştırma hatası (Sayısal değerler okunamadı).";
                            }
                        }
                        else
                        {
                            debugMessage += $"❌ ID Formatı Hatalı (Eksik Veri). Gelen: {finalConversationId}";
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