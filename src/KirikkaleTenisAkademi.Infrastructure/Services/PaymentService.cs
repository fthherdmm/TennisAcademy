using Iyzipay;
using Iyzipay.Model;
using Iyzipay.Request;
using Microsoft.Extensions.Configuration;
using KirikkaleTenisAkademi.Domain.Entities;
using System.Globalization;

namespace KirikkaleTenisAkademi.Infrastructure.Services
{
    public class PaymentService : IPaymentService 
    {
        private readonly IConfiguration _configuration;

        public PaymentService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // 🔴 DEĞİŞİKLİK: 'packetName' parametresi eklendi
        public async Task<CheckoutFormInitialize> GetPaymentForm(AppUser user, decimal price, int creditAmount, string conversationId, string packetName)
        {
            Options options = new Options();
            options.ApiKey = _configuration["Iyzico:ApiKey"];
            options.SecretKey = _configuration["Iyzico:SecretKey"];
            options.BaseUrl = _configuration["Iyzico:BaseUrl"];

            CreateCheckoutFormInitializeRequest request = new CreateCheckoutFormInitializeRequest();
            request.Locale = Locale.TR.ToString();
            request.ConversationId = conversationId;
            request.Price = price.ToString(new CultureInfo("en-US"));
            request.PaidPrice = price.ToString(new CultureInfo("en-US"));
            request.Currency = Currency.TRY.ToString();
            request.BasketId = "B" + DateTime.Now.Ticks;
            request.PaymentGroup = PaymentGroup.PRODUCT.ToString();
            
            // Callback URL
            request.CallbackUrl = $"https://api.winnertenis.com/api/Payment/callback?backupId={conversationId}";

            request.EnabledInstallments = new List<int>() { 2, 3, 6, 9 };

            Buyer buyer = new Buyer();
            buyer.Id = user.Id;
            buyer.Name = string.IsNullOrEmpty(user.FirstName) ? "Misafir" : user.FirstName;
            buyer.Surname = string.IsNullOrEmpty(user.LastName) ? "Kullanici" : user.LastName;
            buyer.GsmNumber = string.IsNullOrEmpty(user.PhoneNumber) ? "+905555555555" : user.PhoneNumber;
            buyer.Email = string.IsNullOrEmpty(user.Email) ? "email@email.com" : user.Email;
            buyer.IdentityNumber = string.IsNullOrEmpty(user.TCKN) ? "11111111111" : user.TCKN;
            buyer.LastLoginDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            buyer.RegistrationDate = user.RegistrationDate.ToString("yyyy-MM-dd HH:mm:ss");
            buyer.RegistrationAddress = "Kırıkkale Tenis Akademi";
            buyer.Ip = "85.85.85.85";
            buyer.City = "Kirikkale";
            buyer.Country = "Turkey";
            buyer.ZipCode = "71100";
            request.Buyer = buyer;

            Address billingAddress = new Address();
            billingAddress.ContactName = user.FirstName + " " + user.LastName;
            billingAddress.City = "Kirikkale";
            billingAddress.Country = "Turkey";
            billingAddress.Description = "Tenis Kortu";
            billingAddress.ZipCode = "71100";
            request.BillingAddress = billingAddress;
            request.ShippingAddress = billingAddress;

            List<BasketItem> basketItems = new List<BasketItem>();
            BasketItem item = new BasketItem();
            item.Id = "KREDI_" + creditAmount;
            
            // 🔴 DEĞİŞİKLİK: Sepet adı artık paketin gerçek adını (Örn: "Yetişkin Grup Paketi") taşıyor
            item.Name = packetName; 
            
            item.Category1 = "Egitim";
            item.ItemType = BasketItemType.VIRTUAL.ToString();
            item.Price = price.ToString(new CultureInfo("en-US"));
            basketItems.Add(item);

            request.BasketItems = basketItems;
            
            return await Task.Run(() => CheckoutFormInitialize.Create(request, options));
        }

        public Task<CheckoutFormInitialize> GetPaymentForm(AppUser user, decimal price, int creditAmount, string conversationId)
        {
            throw new NotImplementedException();
        }

        public async Task<CheckoutForm> RetrievePaymentResult(string token)
        {
            Options options = new Options();
            options.ApiKey = _configuration["Iyzico:ApiKey"];
            options.SecretKey = _configuration["Iyzico:SecretKey"];
            options.BaseUrl = _configuration["Iyzico:BaseUrl"];

            RetrieveCheckoutFormRequest request = new RetrieveCheckoutFormRequest();
            request.Token = token;

            return await Task.Run(() => CheckoutForm.Retrieve(request, options));
        }
    }
}