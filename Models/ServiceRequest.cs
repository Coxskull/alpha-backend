using System;

namespace Alpha.API.Models;

public class ServiceRequest
{
    public Guid Id { get; set; }

    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public string? VehicleInfo { get; set; }

    public string IssueDescription { get; set; } = string.Empty;
    public string ServiceAddress { get; set; } = string.Empty;
    public string Zone { get; set; } = string.Empty;

    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    public Guid? MechanicId { get; set; }

    public string Status { get; set; } = "pending";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}