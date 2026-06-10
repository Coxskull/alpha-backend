namespace Alpha.API.DTOs;

public class CreateOrderDto
{
    public string? CustomerName { get; set; }

    public string? PickupAddress { get; set; }

    public string? DeliveryAddress { get; set; }

    public string? ItemDescription { get; set; }

    public string? Zone { get; set; }
}