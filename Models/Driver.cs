public class Driver
{
    public Guid Id { get; set; }

    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? VehicleType { get; set; }
    public string? PlateNumber { get; set; }

    public string AvailabilityStatus { get; set; } = "available";

    public string? Territory { get; set; }
    public int ActiveJobs { get; set; }
    public double ResponseRate { get; set; } = 100;
    public DateTime? LastSeenAt { get; set; }

    public string? Email { get; set; }
    public string? PasswordHash { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}