using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Alpha.API.Models;

[Table("audit_logs")]
public class AuditLog
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("order_id")]
    public Guid OrderId { get; set; }

    [Column("action")]
    public string Action { get; set; }

    [Column("performed_by")]
    public string? PerformedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}