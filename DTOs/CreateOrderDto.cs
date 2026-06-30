namespace Alpha.API.DTOs;

public class CreateOrderDto
{
    public string CustomerName { get; set; } = string.Empty;
    public string PickupAddress { get; set; } = string.Empty;
    public string DeliveryAddress { get; set; } = string.Empty;
    public string ItemDescription { get; set; } = string.Empty;
    public string Zone { get; set; } = string.Empty;

    // Payment / financial fields
    public string PaymentMethod { get; set; } = "cash";
    public string Currency { get; set; } = "USD";

    public decimal ItemSubtotal { get; set; } = 0;
    public decimal DeliveryFee { get; set; } = 0;
    public decimal ServiceFee { get; set; } = 0;
    public decimal Tax { get; set; } = 0;
    public decimal Discount { get; set; } = 0;
    public decimal TotalAmount { get; set; } = 0;
}