using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace Alpha.API.Models;

[Table("payment_operations")]
public sealed class PaymentOperation
{
    [Column("id")] public Guid Id { get; set; }
    [Column("payment_id")] public Guid PaymentId { get; set; }
    [Column("kind"), MaxLength(16)] public string Kind { get; set; } = string.Empty;
    [Column("idempotency_key"), MaxLength(128)] public string IdempotencyKey { get; set; } = string.Empty;
    [Column("amount")] public decimal Amount { get; set; }
    [Column("currency"), MaxLength(3)] public string Currency { get; set; } = string.Empty;
    [Column("state"), MaxLength(16)] public string State { get; set; } = "reserved";
    [Column("provider_request_id"), MaxLength(38)] public string ProviderRequestId { get; set; } = string.Empty;
    [Column("provider_reference"), MaxLength(128)] public string? ProviderReference { get; set; }
    [Column("provider_parent_reference"), MaxLength(128)] public string ProviderParentReference { get; set; } = string.Empty;
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}
