using System;

namespace Alpha.API.Models;

public class RepairProof
{
    public Guid Id { get; set; }

    public Guid ServiceRequestId { get; set; }

    public Guid? MechanicId { get; set; }

    public string ImageUrl { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}