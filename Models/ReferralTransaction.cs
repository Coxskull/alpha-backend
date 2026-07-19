using System;

namespace Alpha.API.Models;

public class ReferralTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ReferrerId { get; set; }

    public Guid ReferredUserId { get; set; }

    public Guid? OrderId { get; set; }

    public Guid? ServiceRequestId { get; set; }

    public Guid? PaymentId { get; set; }

    public Guid? SourceUserId { get; set; }

    public string EventKey { get; set; } = string.Empty;

    public string TransactionType { get; set; } = string.Empty;

    public string SourceRole { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal EligibleAmount { get; set; }

    public decimal CommissionRate { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "USD";

    public string Status { get; set; } = "pending";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ApprovedAt { get; set; }

    public DateTime? PaidAt { get; set; }
}