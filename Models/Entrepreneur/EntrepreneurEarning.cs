using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Alpha.API.Models.Entrepreneur;

[Table("entrepreneur_earnings")]
public class EntrepreneurEarning
{
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("entrepreneur_user_id")]
    public Guid EntrepreneurUserId { get; set; }

    [Column("recruiter_id")]
    public Guid RecruiterId { get; set; }

    [Column("recruited_provider_id")]
    public Guid RecruitedProviderId { get; set; }

    [Column("provider_role")]
    public string ProviderRole { get; set; } = string.Empty;

    [Column("order_id")]
    public Guid OrderId { get; set; }

    [Column("transaction_id")]
    public string TransactionId { get; set; } = string.Empty;

    [Column("payment_id")]
    public Guid? PaymentId { get; set; }

    [Column("transaction_date")]
    public DateTime TransactionDate { get; set; }

    [Column("alpha_gross_platform_commission")]
    public decimal AlphaGrossPlatformCommission { get; set; }

    [Column("direct_transaction_costs")]
    public decimal DirectTransactionCosts { get; set; }

    [Column("eligible_net_platform_revenue")]
    public decimal EligibleNetPlatformRevenue { get; set; }

    [Column("entrepreneur_percentage")]
    public decimal EntrepreneurPercentage { get; set; }

    [Column("entrepreneur_earnings_amount")]
    public decimal EntrepreneurEarningsAmount { get; set; }

    [Column("currency")]
    public string Currency { get; set; } = "USD";

    [Column("earning_status")]
    public string EarningStatus { get; set; } = "PENDING";

    [Column("refund_adjustment")]
    public decimal RefundAdjustment { get; set; }

    [Column("chargeback_adjustment")]
    public decimal ChargebackAdjustment { get; set; }

    [Column("payout_batch_id")]
    public Guid? PayoutBatchId { get; set; }

    [Column("payout_date")]
    public DateTime? PayoutDate { get; set; }

    [Column("payout_reference")]
    public string? PayoutReference { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [Column("event_key")]
    public string? EventKey { get; set; }
}