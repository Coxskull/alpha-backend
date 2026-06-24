namespace Alpha.API.DTOs;

public class CreateOrderDto
{
    public string CustomerName { get; set; }
    public string PickupAddress { get; set; }
    public string DeliveryAddress { get; set; }
    public string ItemDescription { get; set; }
    public string Zone { get; set; }

    public decimal ItemSubtotal { get; set; }
    public string Currency { get; set; } = "USD"; // USD or MXN
    public string PaymentMethod { get; set; } = "cash";
}