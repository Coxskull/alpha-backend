using System;
using System.Text.Json;

namespace Alpha.API.Models;

public class ReferralTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BeneficiaryUserId { get; set; }

    public Guid SourceUserId { get; set; }

    public Guid? OrderId { get; set; }

    public Guid? ServiceRequestId { get; set; }

    public Guid? PaymentId { get; set; }

    public string TransactionType { get; set; } = string.Empty;

    public string? SourceRole { get; set; }

    public string? SourceDescription { get; set; }

    public decimal GrossAmount { get; set; }

    public decimal CommissionRate { get; set; }

    public decimal CommissionAmount { get; set; }

    public string Currency { get; set; } = "USD";

    public int ReferralLevel { get; set; } = 1;

    public string Status { get; set; } = "pending";

    public DateTime? AvailableAt { get; set; }

    public DateTime? PaidAt { get; set; }

    public JsonDocument? Metadata { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? EventKey { get; set; }

    public string? Description { get; set; }

    public decimal EligibleAmount { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public User? BeneficiaryUser { get; set; }

    public User? SourceUser { get; set; }
}