namespace Alpha.API.Models.Payments;

public class PaymentStatusResult
{
    public bool Success { get; set; }

    public string Status { get; set; } = "";

    public decimal GatewayFee { get; set; }

    public string Reference { get; set; } = "";
}