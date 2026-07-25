using System;

namespace Alpha.API.DTOs;

public class CreatePayMongoCheckoutDto
{
    public Guid OrderId { get; set; }
}

public class VerifyPayMongoCheckoutDto
{
    public Guid OrderId { get; set; }
    public string CheckoutSessionId { get; set; } = string.Empty;
}