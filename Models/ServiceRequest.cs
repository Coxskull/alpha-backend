using System;

namespace Alpha.API.Models;

public class ServiceRequest
{
    public Guid Id { get; set; }

    public Guid? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }

    public string? VehicleInfo { get; set; }
    public string IssueDescription { get; set; } = string.Empty;

    public string ServiceAddress { get; set; } = string.Empty;
    public string Zone { get; set; } = string.Empty;

    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    public Guid? ProviderId { get; set; }
    public Guid? MechanicId { get; set; }
    public Guid? DriverId { get; set; }
    public Guid? PartsRequestId { get; set; }

    public string Status { get; set; } = "new_request";

    public string? PartsRequestNote { get; set; }
    public string? ProofImageUrl { get; set; }
    public string? RejectionReason { get; set; }

    public decimal FinalAmount { get; set; }
    public string PaymentStatus { get; set; } = "unpaid";

    public DateTime? ProviderAcceptedAt { get; set; }
    public DateTime? MechanicAcceptedAt { get; set; }
    public DateTime? DriverAssignedAt { get; set; }

    public DateTime? AcceptedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? ProofUploadedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}