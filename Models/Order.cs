using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Alpha.API.Models;

[Table("orders")]
public class Order
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("order_number")]
    public string OrderNumber { get; set; } = string.Empty;

    [Column("customer_name")]
    public string CustomerName { get; set; } = string.Empty;

    [Column("pickup_address")]
    public string PickupAddress { get; set; } = string.Empty;

    [Column("delivery_address")]
    public string DeliveryAddress { get; set; } = string.Empty;

    [Column("item_description")]
    public string ItemDescription { get; set; } = string.Empty;

    [Column("zone")]
    public string Zone { get; set; } = string.Empty;

    [Column("supplier_id")]
    public Guid? SupplierId { get; set; }

    [Column("driver_id")]
    public Guid? DriverId { get; set; }

    [Column("status")]
    public string Status { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
    public Supplier? Supplier { get; set; }

    public Driver? Driver { get; set; }
    public DeliveryProof? DeliveryProof { get; set; }

    public Guid? CustomerId { get; set; }

    public User? CustomerUser { get; set; }

    [Column("country_code")]
    public string CountryCode { get; set; } = "MX";

    [Column("currency")]
    public string Currency { get; set; } = "MXN";
}