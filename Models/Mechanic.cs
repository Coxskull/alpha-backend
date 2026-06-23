using System;

namespace Alpha.API.Models;

public class Mechanic
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }

    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }

    public string? ServiceArea { get; set; }
    public string AvailabilityStatus { get; set; } = "available";

    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public decimal ServiceRadiusKm { get; set; } = 10;

    public int ActiveJobs { get; set; } = 0;
    public decimal ResponseRate { get; set; } = 100;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}