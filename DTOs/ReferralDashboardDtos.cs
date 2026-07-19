using System;
using System.Collections.Generic;

namespace Alpha.API.DTOs;

public class ReferralDashboardDto
{
    public string ReferralCode { get; set; } = string.Empty;

    public string ReferralLink { get; set; } = string.Empty;

    public ReferralSummaryDto Summary { get; set; } = new();

    public List<ReferralMemberDto> DirectMembers { get; set; } = new();

    public List<ReferralMemberDto> NetworkMembers { get; set; } = new();

    public List<ReferralTransactionDto> RecentTransactions { get; set; } = new();
}

public class ReferralSummaryDto
{
    public int DirectMembers { get; set; }

    public int TotalNetworkMembers { get; set; }

    public int ActiveNetworkMembers { get; set; }

    public int NetworkTransactions { get; set; }

    public decimal PendingEarnings { get; set; }

    public decimal AvailableEarnings { get; set; }

    public decimal PaidEarnings { get; set; }

    public decimal LifetimeEarnings { get; set; }

    public string Currency { get; set; } = "MXN";
}

public class ReferralMemberDto
{
    public Guid UserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string? ReferralCode { get; set; }

    public int Level { get; set; }

    public Guid? ReferredByUserId { get; set; }

    public string? ReferredByName { get; set; }

    public DateTime JoinedAt { get; set; }

    public int TransactionCount { get; set; }

    public decimal GeneratedVolume { get; set; }

    public decimal GeneratedCommission { get; set; }

    public bool IsActive { get; set; }
}

public class ReferralTransactionDto
{
    public Guid Id { get; set; }

    public Guid SourceUserId { get; set; }

    public string SourceUserName { get; set; } = string.Empty;

    public string SourceRole { get; set; } = string.Empty;

    public string TransactionType { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int ReferralLevel { get; set; }

    public decimal GrossAmount { get; set; }

    public decimal CommissionRate { get; set; }

    public decimal CommissionAmount { get; set; }

    public string Currency { get; set; } = "MXN";

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}