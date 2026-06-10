using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Alpha.API.Models;

[Table("status_history")]
public class StatusHistory
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("order_id")]
    public Guid OrderId { get; set; }

    [Column("status")]
    public string Status { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}