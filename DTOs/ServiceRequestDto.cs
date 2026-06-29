using System;

namespace Alpha.API.DTOs;

public class ServiceRequestDto
{
    public Guid Id { get; set; }

    public Guid? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }

    public string? VehicleInfo { get; set; }
    public string IssueDescription { get; set; } = string.Empty;

    public string ServiceAddress { get; set; } = string.Empty;
    public string Zone { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public decimal FinalAmount { get; set; }
    public string PaymentStatus { get; set; } = "unpaid";

    public Guid? ProviderId { get; set; }
    public string? ProviderName { get; set; }

    public Guid? MechanicId { get; set; }
    public string? MechanicName { get; set; }

    public Guid? DriverId { get; set; }
    public string? DriverName { get; set; }

    public string? PartsRequestNote { get; set; }
    public string? ProofImageUrl { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}