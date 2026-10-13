using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace Alpha.API.Models;
[Table("financial_recovery_releases")]
public sealed class FinancialRecoveryRelease
{
    [Column("id")] public Guid Id { get; set; }
    [Column("review_id")] public Guid ReviewId { get; set; }
    [Column("allocation_id")] public Guid AllocationId { get; set; }
    [Column("reviewer_id")] public Guid ReviewerId { get; set; }
    [Column("source_fingerprint"), MaxLength(64)] public string SourceFingerprint { get; set; } = "";
    [Column("evidence"), MaxLength(4000)] public string Evidence { get; set; } = "";
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}
