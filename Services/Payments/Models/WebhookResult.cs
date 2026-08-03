namespace Alpha.API.Services.Payments.Models;

public class WebhookResult
{
    public bool Success { get; set; }

    public string GatewayPaymentId { get; set; } = "";

    public string Status { get; set; } = "";

    public string TransactionReference { get; set; } = "";

    public string RawPayload { get; set; } = "";
}