using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace Alpha.API.Models;

[Table("refund_allocations")]
public sealed class RefundAllocation
{
    [Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("operation_id")] public Guid OperationId { get; set; }
    [Column("source_kind"), MaxLength(16)] public string SourceKind { get; set; } = string.Empty;
    [Column("source_id")] public Guid SourceId { get; set; }
    [Column("payee_type"), MaxLength(16)] public string PayeeType { get; set; } = string.Empty;
    [Column("payee_id")] public Guid PayeeId { get; set; }
    [Column("currency"), MaxLength(3)] public string Currency { get; set; } = string.Empty;
    [Column("amount")] public decimal Amount { get; set; }
    [Column("future_recovery")] public bool FutureRecovery { get; set; }
    [Column("recovered_amount")] public decimal RecoveredAmount { get; set; }
    [Column("reviewed_by")] public Guid ReviewedBy { get; set; }
    [Column("evidence"), MaxLength(1000)] public string Evidence { get; set; } = string.Empty;
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Table("payout_recovery_offsets")]
public sealed class PayoutRecoveryOffset
{
    [Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("allocation_id")] public Guid AllocationId { get; set; }
    [Column("target_kind"), MaxLength(16)] public string TargetKind { get; set; } = string.Empty;
    [Column("target_id")] public Guid TargetId { get; set; }
    [Column("amount")] public decimal Amount { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
