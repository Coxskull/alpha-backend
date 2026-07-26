using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Alpha.API.Models;

[Table("products")]
public class Product
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("supplier_id")]
    public Guid SupplierId { get; set; }

    [Column("part_number")]
    public string PartNumber { get; set; } = string.Empty;

    [Column("brand")]
    public string Brand { get; set; } = string.Empty;

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    public string Description { get; set; } = string.Empty;

    [Column("image_url")]
    public string ImageUrl { get; set; } = string.Empty;

    [Column("price")]
    public decimal Price { get; set; }

    [Column("quantity_available")]
    public int QuantityAvailable { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("image_base64")]
    public string? ImageBase64 { get; set; }

    [Column("payment_gateway")]
    public string? PaymentGateway { get; set; }

    [Column("checkout_url")]
    public string? CheckoutUrl { get; set; }

    [Column("gateway_payment_id")]
    public string? GatewayPaymentId { get; set; }

    [Column("gateway_checkout_session_id")]
    public string? GatewayCheckoutSessionId { get; set; }

    [Column("failure_reason")]
    public string? FailureReason { get; set; }

    [Column("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    public string Currency { get; set; } = "USD";

    public string CountryCode { get; set; } = "US";
}