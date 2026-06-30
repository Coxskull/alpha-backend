// DTOs/PayPalDtos.cs
using System;

namespace Alpha.API.DTOs;

public class CreatePayPalOrderDto
{
    public Guid OrderId { get; set; }
}

public class CapturePayPalOrderDto
{
    public Guid OrderId { get; set; }
    public string PayPalOrderId { get; set; } = string.Empty;
}