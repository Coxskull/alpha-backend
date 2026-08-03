using System.Threading.Tasks;

namespace Alpha.API.Services.Payments.Interfaces;

public interface IPaymentGateway
{
    string Name { get; }

    Task<CreatePaymentResponse> CreatePaymentAsync(
        CreatePaymentRequest request);

    Task<PaymentStatusResponse> GetStatusAsync(
        string gatewayPaymentId);

    Task<RefundResponse> RefundAsync(
        RefundRequest request);

    Task<WebhookResult> ProcessWebhookAsync(
        HttpRequest request);
}