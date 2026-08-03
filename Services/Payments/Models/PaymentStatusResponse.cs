namespace Alpha.API.Services.Payments.Models;

public class PaymentStatusResponse
{
    public bool Success { get; set; }

    public string Status { get; set; } = "";

    public decimal GatewayFee { get; set; }

    public string TransactionReference { get; set; } = "";

    public string RawResponse { get; set; } = "";
}