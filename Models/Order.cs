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
    public string OrderNumber { get; set; }

    [Column("customer_name")]
    public string CustomerName { get; set; }

    [Column("pickup_address")]
    public string PickupAddress { get; set; }

    [Column("delivery_address")]
    public string DeliveryAddress { get; set; }

    [Column("item_description")]
    public string ItemDescription { get; set; }

    [Column("zone")]
    public string Zone { get; set; }

    [Column("supplier_id")]
    public Guid? SupplierId { get; set; }

    [Column("driver_id")]
    public Guid? DriverId { get; set; }

    [Column("status")]
    public string Status { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
    public Supplier? Supplier { get; set; }

    public Driver? Driver { get; set; }
}