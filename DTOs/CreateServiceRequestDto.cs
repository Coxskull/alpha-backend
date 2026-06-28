using System;

namespace Alpha.API.DTOs;

public class CreateServiceRequestDto
{
    public Guid? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public string? VehicleInfo { get; set; }
    public string IssueDescription { get; set; } = string.Empty;
    public string ServiceAddress { get; set; } = string.Empty;
    public string Zone { get; set; } = string.Empty;
    public decimal FinalAmount { get; set; }
}