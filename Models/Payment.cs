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

    [Column("payment_gateway")]
    public string? PaymentGateway { get; set; }

    [Column("gateway_checkout_session_id")]
    public string? GatewayCheckoutSessionId { get; set; }

    [Column("gateway_payment_id")]
    public string? GatewayPaymentId { get; set; }

    [Column("checkout_url")]
    public string? CheckoutUrl { get; set; }

    [Column("failure_reason")]
    public string? FailureReason { get; set; }

    [Column("gateway_fee")]
    public decimal GatewayFee { get; set; }

    [Column("gateway_response")]
    public string? GatewayResponse { get; set; }

    [Column("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    [Column("paid_at")]
    public DateTime? PaidAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("refunded_amount")]
    public decimal RefundedAmount { get; set; }

    [Column("refund_status")]
    public string RefundStatus { get; set; } = "none";

    [Column("refund_reference")]
    public string? RefundReference { get; set; }

    [Column("refunded_at")]
    public DateTime? RefundedAt { get; set; }
}