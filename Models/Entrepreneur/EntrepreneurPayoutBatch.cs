using System;

namespace Alpha.API.Models.Entrepreneur;

public class EntrepreneurPayoutBatch
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime SettlementDate { get; set; }

    public string Currency { get; set; } = "USD";

    public decimal TotalAmount { get; set; }

    public int EarningCount { get; set; }

    public string Status { get; set; } = "PENDING";

    public string? PayoutReference { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? PaidAt { get; set; }

    public Guid? ProcessedByUserId { get; set; }
}