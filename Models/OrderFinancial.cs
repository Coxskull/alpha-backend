using System;

namespace Alpha.API.Models;

public class OrderFinancial
{
    public Guid Id { get; set; }

    public Guid? OrderId { get; set; }
    public Guid? ServiceRequestId { get; set; }

    public decimal CustomerPaid { get; set; }
    public decimal SupplierAmount { get; set; }
    public decimal DriverAmount { get; set; }
    public decimal MechanicAmount { get; set; }
    public decimal AlphaPlatformFee { get; set; }

    public string FinancialStatus { get; set; } = "pending_review";
    public string PayoutStatus { get; set; } = "manual_review";

    public string? CompletionProofUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}