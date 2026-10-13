using System.ComponentModel.DataAnnotations.Schema;

namespace Alpha.API.Models;

[Table("community_builder_profiles")]
public sealed class CommunityBuilderProfile
{
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("home_city")]
    public string HomeCity { get; set; } = string.Empty;

    [Column("home_state")]
    public string? HomeState { get; set; }

    [Column("country")]
    public string Country { get; set; } = "MX";

    [Column("builder_status")]
    public string BuilderStatus { get; set; } = "active";

    [Column("total_cities_connected")]
    public int TotalCitiesConnected { get; set; } = 1;

    [Column("approved_at")]
    public DateTime? ApprovedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

}
