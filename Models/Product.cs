using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Alpha.API.Models;

[Table("products", Schema = "public")]
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
    public string ImageUrl { get; set; } =
        "/uploads/products/default-product.png";

    [Column("price")]
    public decimal Price { get; set; }

    [Column("quantity_available")]
    public int QuantityAvailable { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("low_stock_threshold")]
    public int LowStockThreshold { get; set; } = 5;

    [Column("image_base64")]
    public string? ImageBase64 { get; set; }

    [Column("currency")]
    public string Currency { get; set; } = "MXN";

    [Column("country_code")]
    public string CountryCode { get; set; } = "MX";
}