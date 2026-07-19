using System;

namespace Alpha.API.Models;

public class ReferralCommissionRate
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string TransactionType { get; set; } = string.Empty;

    public string SourceRole { get; set; } = string.Empty;

    public decimal Rate { get; set; }

    public decimal? FixedAmount { get; set; }

    public string Currency { get; set; } = "USD";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}