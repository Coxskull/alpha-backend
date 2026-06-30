using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Alpha.API.Models;

[Table("settlement_queue")]
public class SettlementQueue
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("order_financial_id")]
    public Guid OrderFinancialId { get; set; }

    [Column("payee_type")]
    public string PayeeType { get; set; } = string.Empty;

    [Column("payee_id")]
    public Guid? PayeeId { get; set; }

    [Column("amount")]
    public decimal Amount { get; set; }

    [Column("status")]
    public string Status { get; set; } = "pending_review";

    [Column("reviewed_by")]
    public string? ReviewedBy { get; set; }

    [Column("reviewed_at")]
    public DateTime? ReviewedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}