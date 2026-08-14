using System;

namespace Alpha.API.Models;

public class AutoPartsCommissionAuditLog
{
    public Guid Id { get; set; }

    public Guid? PolicyId { get; set; }

    public int? PolicyVersion { get; set; }

    public string Action { get; set; } = string.Empty;

    public Guid? PerformedByUserId { get; set; }

    public string? BeforeSnapshot { get; set; }

    public string? AfterSnapshot { get; set; }

    public string? Reason { get; set; }

    public DateTime CreatedAt { get; set; }
}