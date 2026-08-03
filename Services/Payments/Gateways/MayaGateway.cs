using Alpha.API.Services.Payments.Interfaces;
using Alpha.API.Services.Payments.Models;
using System;
using System.Threading.Tasks;

namespace Alpha.API.Services.Payments.Gateways;

public class MayaGateway : IPaymentGateway
{
    public string Name => "maya";

    public Task<CreatePaymentResponse> CreatePaymentAsync(CreatePaymentRequest request)
        => throw new NotImplementedException();

    public Task<PaymentStatusResponse> GetStatusAsync(string gatewayPaymentId)
        => throw new NotImplementedException();

    public Task<RefundResponse> RefundAsync(RefundRequest request)
        => throw new NotImplementedException();

    public Task<WebhookResult> ProcessWebhookAsync(HttpRequest request)
        => throw new NotImplementedException();
}