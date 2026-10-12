using System.ComponentModel.DataAnnotations.Schema;

namespace Alpha.API.Models;

[Table("community_builder_cities")]
public sealed class CommunityBuilderCity
{
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("builder_user_id")]
    public Guid BuilderUserId { get; set; }

    [Column("city")]
    public string City { get; set; } = string.Empty;

    [Column("state")]
    public string? State { get; set; }

    [Column("country")]
    public string Country { get; set; } = "MX";

    [Column("member_count")]
    public int MemberCount { get; set; }

    [Column("active_member_count")]
    public int ActiveMemberCount { get; set; }

    [Column("completed_transaction_count")]
    public int CompletedTransactionCount { get; set; }

    [Column("connected_at")]
    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;

}
