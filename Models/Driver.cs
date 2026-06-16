namespace Alpha.API.Models;

public class Driver
{
    public Guid Id { get; set; }

    public string FullName { get; set; } = "";

    public string? PhoneNumber { get; set; }

    public string? VehicleType { get; set; }

    public string? PlateNumber { get; set; }

    public string AvailabilityStatus { get; set; } = "available";

    public string Territory { get; set; } = "";

    public int ActiveJobs { get; set; } = 0;

    public double ResponseRate { get; set; } = 100;

    public DateTime? LastSeenAt { get; set; }

    public DateTime CreatedAt { get; set; }
}