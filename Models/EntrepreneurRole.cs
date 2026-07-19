using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Alpha.API.Models;

[Table("entrepreneur_roles")]
public class EntrepreneurRole
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("role_key")]
    public string RoleKey { get; set; } = string.Empty;

    [Column("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Column("icon")]
    public string? Icon { get; set; }

    [Column("is_operational_role")]
    public bool IsOperationalRole { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("sort_order")]
    public int SortOrder { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}