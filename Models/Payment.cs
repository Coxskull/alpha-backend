using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Alpha.API.Models;

[Table("payments")]
public class Payment
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("order_id")]
    public Guid OrderId { get; set; }

    [Column("amount")]
    public decimal Amount { get; set; }

    [Column("currency")]
    public string Currency { get; set; } = "USD";

    [Column("payment_method")]
    public string PaymentMethod { get; set; } = "cash";

    [Column("payment_status")]
    public string PaymentStatus { get; set; } = "pending";

    [Column("transaction_reference")]
    public string? TransactionReference { get; set; }

    [Column("paid_at")]
    public DateTime? PaidAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}