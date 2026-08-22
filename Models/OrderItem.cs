using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Alpha.API.Models;

[Table("order_items")]
public class OrderItem
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public Guid ProductId { get; set; }

    public Guid SupplierId { get; set; }

    public int Quantity { get; set; }

    public decimal? UnitPrice { get; set; }

    public Order? Order { get; set; }

    public Product? Product { get; set; }

    public Supplier? Supplier { get; set; }
}