using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Alpha.API.Models.Entrepreneur;

[Table("entrepreneur_earning_adjustments")]
public class EntrepreneurEarningAdjustment
{
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("entrepreneur_earning_id")]
    public Guid EntrepreneurEarningId { get; set; }

    [Column("adjustment_type")]
    public string AdjustmentType { get; set; } = string.Empty;

    [Column("amount")]
    public decimal Amount { get; set; }

    [Column("currency")]
    public string Currency { get; set; } = "USD";

    [Column("reason")]
    public string Reason { get; set; } = string.Empty;

    [Column("related_payment_id")]
    public Guid? RelatedPaymentId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}