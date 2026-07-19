using System.ComponentModel.DataAnnotations;

namespace Alpha.API.Models;

public class User
{
    [Key]
    public Guid Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string Role { get; set; } = "customer";

    public string PasswordHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;

    public string? ReferralCode { get; set; }

    public Guid? ReferredByUserId { get; set; }

    public DateTime? ReferralJoinedAt { get; set; }

    public User? ReferredByUser { get; set; }

    public ICollection<User> DirectReferrals { get; set; } =
        new List<User>();

    public ICollection<ReferralTransaction> ReferralEarnings { get; set; } =
        new List<ReferralTransaction>();

    public ICollection<ReferralTransaction> GeneratedReferralTransactions { get; set; } =
        new List<ReferralTransaction>();
}