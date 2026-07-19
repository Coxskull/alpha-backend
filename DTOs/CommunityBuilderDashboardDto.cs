using System;
using System.Collections.Generic;

namespace Alpha.API.DTOs;

public class CommunityBuilderDashboardDto
{
    public Guid UserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string ReferralCode { get; set; } = string.Empty;

    public string ReferralLink { get; set; } = string.Empty;

    public int DirectMembers { get; set; }

    public int ActiveMembers { get; set; }

    public decimal PendingRewards { get; set; }

    public decimal AvailableRewards { get; set; }

    public decimal PaidRewards { get; set; }

    public string Currency { get; set; } = "USD";

    public List<CommunityBuilderMemberDto>
        Members
    { get; set; } = [];

    public List<NetworkActivityDto>
        RecentActivities
    { get; set; } = [];

    public List<CityNetworkDto>
        Cities
    { get; set; } = [];
}

public class CommunityBuilderMemberDto
{
    public Guid UserId { get; set; }

    public string FullName { get; set; } =
        string.Empty;

    public string PrimaryRole { get; set; } =
        string.Empty;

    public string City { get; set; } =
        string.Empty;

    public bool IsBusinessActive { get; set; }

    public decimal GeneratedRewards { get; set; }

    public DateTime? JoinedAt { get; set; }
}

public class NetworkActivityDto
{
    public Guid Id { get; set; }

    public string MemberName { get; set; } =
        string.Empty;

    public string TransactionType { get; set; } =
        string.Empty;

    public string Description { get; set; } =
        string.Empty;

    public decimal EligibleAmount { get; set; }

    public decimal RewardAmount { get; set; }

    public string Currency { get; set; } =
        "USD";

    public string Status { get; set; } =
        string.Empty;

    public DateTime CreatedAt { get; set; }
}

public class CityNetworkDto
{
    public string City { get; set; } =
        string.Empty;

    public int TotalMembers { get; set; }

    public int ActiveMembers { get; set; }

    public decimal GeneratedRewards { get; set; }
}