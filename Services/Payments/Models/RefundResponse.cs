namespace Alpha.API.Services.Payments.Models;

public class RefundResponse
{
    public bool Success { get; set; }

    public string RefundReference { get; set; } = "";

    public string Error { get; set; } = "";
}