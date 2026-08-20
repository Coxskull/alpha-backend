using System;

namespace Alpha.API.Models.Entrepreneur;

public class EntrepreneurEarning
{
    public Guid id { get; set; } = Guid.NewGuid();

    public Guid EntrepreneurUserId { get; set; }

    public Guid RecruiterId { get; set; }

    public Guid RecruitedProviderId { get; set; }

    public string ProviderRole { get; set; } = string.Empty;

    public Guid OrderId { get; set; }

    public string TransactionId { get; set; } = string.Empty;

    public Guid? PaymentId { get; set; }

    public DateTime TransactionDate { get; set; }

    // Marketplace economics
    public decimal AlphaGrossPlatformCommission { get; set; }

    // Direct transaction costs only
    public decimal DirectTransactionCosts { get; set; }

    // AlphaGrossPlatformCommission - DirectTransactionCosts
    public decimal EligibleNetPlatformRevenue { get; set; }

    // 0.05 = 5%
    public decimal EntrepreneurPercentage { get; set; }

    public decimal EntrepreneurEarningsAmount { get; set; }

    public string Currency { get; set; } = "USD";

    public string EarningStatus { get; set; } = "PENDING";

    public decimal RefundAdjustment { get; set; }

    public decimal ChargebackAdjustment { get; set; }

    public Guid? PayoutBatchId { get; set; }

    public DateTime? PayoutDate { get; set; }

    public string? PayoutReference { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}