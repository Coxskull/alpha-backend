namespace Alpha.API.Models;

public class Driver
{
    public Guid Id { get; set; }

    public string FullName { get; set; } = "";

    public string? PhoneNumber { get; set; }

    public string? VehicleType { get; set; }

    public string? PlateNumber { get; set; }

    public string AvailabilityStatus { get; set; } = "available";

    public DateTime CreatedAt { get; set; }
}