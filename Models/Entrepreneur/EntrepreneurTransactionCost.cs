using System;

namespace Alpha.API.Models.Entrepreneur;

public class EntrepreneurTransactionCost
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrderId { get; set; }

    public Guid? PaymentId { get; set; }

    public string CostType { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "USD";

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}