using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Alpha.API.Models;

[Table("order_financials")]
public class OrderFinancial
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("order_id")]
    public Guid OrderId { get; set; }

    [Column("currency")]
    public string Currency { get; set; } = "USD"; // USD or MXN

    [Column("exchange_rate")]
    public decimal ExchangeRate { get; set; } = 1;

    [Column("item_subtotal")]
    public decimal ItemSubtotal { get; set; }

    [Column("delivery_fee")]
    public decimal DeliveryFee { get; set; }

    [Column("service_fee")]
    public decimal ServiceFee { get; set; }

    [Column("tax")]
    public decimal Tax { get; set; }

    [Column("discount")]
    public decimal Discount { get; set; }

    [Column("total_amount")]
    public decimal TotalAmount { get; set; }

    [Column("supplier_earning")]
    public decimal SupplierEarning { get; set; }

    [Column("driver_earning")]
    public decimal DriverEarning { get; set; }

    [Column("company_revenue")]
    public decimal CompanyRevenue { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}