namespace Alpha.API.DTOs;

public class CreateOrderDto
{
    public string CustomerName { get; set; } = string.Empty;
    public string PickupAddress { get; set; } = string.Empty;
    public string DeliveryAddress { get; set; } = string.Empty;
    public string ItemDescription { get; set; } = string.Empty;
    public string Zone { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = "cash";
    public string Currency { get; set; } = "USD";

    public List<CreateOrderItemDto> Items { get; set; } = new();
}

public class CreateOrderItemDto
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
}