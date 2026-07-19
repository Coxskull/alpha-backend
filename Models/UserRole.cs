using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Alpha.API.Models;

[Table("user_roles")]
public class UserRole
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("role_key")]
    public string RoleKey { get; set; } = string.Empty;

    [Column("status")]
    public string Status { get; set; } = "pending";

    [Column("is_primary")]
    public bool IsPrimary { get; set; }

    [Column("activated_at")]
    public DateTime? ActivatedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;

    public EntrepreneurRole Role { get; set; } = null!;
}