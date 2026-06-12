using System;
namespace Alpha.API.DTOs;
public class OrderDetailsDto
{
    public Guid Id { get; set; }

    public string OrderNumber { get; set; }

    public string CustomerName { get; set; }

    public string ItemDescription { get; set; }

    public string PickupAddress { get; set; }

    public string DeliveryAddress { get; set; }

    public string Status { get; set; }

    public string SupplierName { get; set; }

    public string DriverName { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}