using System;

namespace Alpha.API.Models;

public class PartsRequest
{
    public Guid Id { get; set; }

    public Guid ServiceRequestId { get; set; }

    public Guid? MechanicId { get; set; }

    public string PartDescription { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public string Status { get; set; } = "requested";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}