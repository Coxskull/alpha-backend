using System;

namespace Alpha.API.Services.Payments.Models;

public class CreatePaymentRequest
{
    public Guid OrderId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "";

    public string Description { get; set; } = "";

    public string CustomerEmail { get; set; } = "";

    public string CustomerName { get; set; } = "";

    public string SuccessUrl { get; set; } = "";

    public string CancelUrl { get; set; } = "";

    public string Gateway { get; set; } = "";
}