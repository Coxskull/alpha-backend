namespace Alpha.API.Services.Payments.Models;

public class CreatePaymentResponse
{
    public bool Success { get; set; }

    public string CheckoutUrl { get; set; } = "";

    public string GatewayPaymentId { get; set; } = "";

    public string CheckoutSessionId { get; set; } = "";

    public string GatewayReference { get; set; } = "";

    public string RawResponse { get; set; } = "";

    public string Error { get; set; } = "";
}