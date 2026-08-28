using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Alpha.API.Models.Entrepreneur;

[Table("entrepreneur_transaction_costs")]
public class EntrepreneurTransactionCost
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("order_id")]
    public Guid OrderId { get; set; }

    [Column("payment_id")]
    public Guid? PaymentId { get; set; }

    [Column("cost_type")]
    public string CostType { get; set; } = string.Empty;

    [Column("amount")]
    public decimal Amount { get; set; }

    [Column("currency")]
    public string Currency { get; set; } = "USD";

    [Column("description")]
    public string? Description { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}