using KirikkaleTenisAkademi.Domain.Entities;
using Iyzipay.Model;

namespace KirikkaleTenisAkademi.Infrastructure.Services
{
    public interface IPaymentService
    {
        // DİKKAT: Burada sadece "Task<...>" var. Çift Task YOK.
        Task<CheckoutFormInitialize> GetPaymentForm(AppUser user, decimal price, int creditAmount, string conversationId);
        
        Task<CheckoutForm> RetrievePaymentResult(string token);
    }
}