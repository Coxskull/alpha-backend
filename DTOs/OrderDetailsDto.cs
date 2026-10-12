using System;
namespace Alpha.API.DTOs;
public class OrderDetailsDto
{
    public Guid Id { get; set; }

    public required string OrderNumber { get; set; }

    public required string CustomerName { get; set; }

    public required string ItemDescription { get; set; }

    public required string PickupAddress { get; set; }

    public required string DeliveryAddress { get; set; }

    public required string Status { get; set; }

    public string? SupplierName { get; set; }

    public string? DriverName { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
