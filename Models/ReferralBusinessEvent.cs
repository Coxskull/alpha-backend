using System;

namespace Alpha.API.Models;

public class ReferralBusinessEvent
{
    public string EventKey { get; set; } =
        string.Empty;

    public string TransactionType { get; set; } =
        string.Empty;

    public Guid SourceUserId { get; set; }

    public string SourceRole { get; set; } =
        string.Empty;

    public Guid? OrderId { get; set; }

    public Guid? ServiceRequestId { get; set; }

    public Guid? PaymentId { get; set; }

    public decimal EligibleAmount { get; set; }

    public string Currency { get; set; } =
        "USD";

    public string? Description { get; set; }

    public DateTime OccurredAt { get; set; } =
        DateTime.UtcNow;
}