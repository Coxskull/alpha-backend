namespace Alpha.API.Models.Payments;

public class PaymentResult
{
    public bool Success { get; set; }

    public string PaymentId { get; set; } = "";

    public string CheckoutUrl { get; set; } = "";

    public string TransactionReference { get; set; } = "";

    public string RawResponse { get; set; } = "";

    public string Error { get; set; } = "";
}