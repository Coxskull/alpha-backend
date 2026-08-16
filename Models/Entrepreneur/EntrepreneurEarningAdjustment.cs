using System;

namespace Alpha.API.Models.Entrepreneur;

public class EntrepreneurEarningAdjustment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EntrepreneurEarningId { get; set; }

    public string AdjustmentType { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "USD";

    public string Reason { get; set; } = string.Empty;

    public Guid? RelatedPaymentId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}