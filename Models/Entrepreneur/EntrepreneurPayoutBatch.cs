using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Alpha.API.Models.Entrepreneur;

[Table("entrepreneur_payout_batches")]
public class EntrepreneurPayoutBatch
{
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("settlement_date")]
    public DateTime SettlementDate { get; set; }

    [Column("currency")]
    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    [Column("total_amount")]
    public decimal TotalAmount { get; set; }

    [Column("earning_count")]
    public int EarningCount { get; set; }

    [Column("status")]
    [MaxLength(30)]
    public string Status { get; set; } = "PENDING";

    [Column("payout_reference")]
    [MaxLength(200)]
    public string? PayoutReference { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("paid_at")]
    public DateTime? PaidAt { get; set; }

    [Column("processed_by_user_id")]
    public Guid? ProcessedByUserId { get; set; }
}
