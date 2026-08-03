namespace Alpha.API.Services.Payments.Models;

public class RefundRequest
{
    public string GatewayPaymentId { get; set; } = "";

    public decimal Amount { get; set; }
}