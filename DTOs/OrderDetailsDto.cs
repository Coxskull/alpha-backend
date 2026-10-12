using System;
namespace Alpha.API.DTOs;
public class OrderDetailsDto
{
    public Guid Id { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public string ItemDescription { get; set; } = string.Empty;

    public string PickupAddress { get; set; } = string.Empty;

    public string DeliveryAddress { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string SupplierName { get; set; } = string.Empty;

    public string DriverName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}