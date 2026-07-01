using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Alpha.API.Models;

[Table("invoices")]
public class Invoice
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("order_id")]
    public Guid? OrderId { get; set; }

    [Column("invoice_number")]
    public string InvoiceNumber { get; set; } = string.Empty;

    [Column("subtotal")]
    public decimal Subtotal { get; set; }

    [Column("tax")]
    public decimal Tax { get; set; }

    [Column("total")]
    public decimal Total { get; set; }

    [Column("currency")]
    public string Currency { get; set; } = "USD";

    [Column("issued_at")]
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
}