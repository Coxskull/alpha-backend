using Alpha.API.Models.Payments;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace Alpha.API.Services.Providers;

public interface IPaymentProvider
{
    string Name { get; }

    Task<PaymentResult> CreatePaymentAsync(
        PaymentRequest request);

    Task<PaymentStatusResult> GetStatusAsync(
        string paymentId);

    Task<bool> RefundAsync(
        string paymentId,
        decimal amount);

    Task<bool> HandleWebhookAsync(
        HttpRequest request);
}