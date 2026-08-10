using System;

namespace Alpha.API.Models.Payments;

public class PaymentRequest
{
    public Guid OrderId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "";

    public string CustomerEmail { get; set; } = "";

    public string CustomerName { get; set; } = "";

    public string Gateway { get; set; } = "";

    public string SuccessUrl { get; set; } = "";

    public string CancelUrl { get; set; } = "";
}