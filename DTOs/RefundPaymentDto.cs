using System;

namespace Alpha.API.DTOs;

public class RefundPaymentDto
{
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
}