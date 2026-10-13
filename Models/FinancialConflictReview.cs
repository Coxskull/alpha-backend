using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace Alpha.API.Models;

[Table("financial_conflict_reviews")]
public sealed class FinancialConflictReview
{
    [Column("id")] public Guid Id { get; set; }
    [Column("payment_id")] public Guid PaymentId { get; set; }
    [Column("order_id")] public Guid OrderId { get; set; }
    [Column("first_reviewer_id")] public Guid FirstReviewerId { get; set; }
    [Column("idempotency_key"), MaxLength(128)] public string IdempotencyKey { get; set; } = "";
    [Column("evidence"), MaxLength(4000)] public string Evidence { get; set; } = "";
    [Column("snapshot_hash"), MaxLength(64)] public string SnapshotHash { get; set; } = "";
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

[Table("financial_conflict_approvals")]
public sealed class FinancialConflictApproval
{
    [Column("id")] public Guid Id { get; set; }
    [Column("review_id")] public Guid ReviewId { get; set; }
    [Column("reviewer_id")] public Guid ReviewerId { get; set; }
    [Column("evidence"), MaxLength(4000)] public string Evidence { get; set; } = "";
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}
