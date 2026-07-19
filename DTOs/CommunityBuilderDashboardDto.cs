using System.Collections.Generic;

namespace Alpha.API.DTOs;

public class CommunityBuilderDashboardDto
{
    public string ReferralCode { get; set; } = string.Empty;
    public string ReferralLink { get; set; } = string.Empty;

    public CommunityBuilderSummaryDto Summary { get; set; } = new();

    public List<CommunityBuilderMemberDto> MyNetwork { get; set; } = new();
    public List<NetworkActivityDto> RecentActivity { get; set; } = new();
    public List<CityNetworkDto> Cities { get; set; } = new();
    public List<ReferralTransactionDto> RecentRewards { get; set; } = new();
}

public class CommunityBuilderSummaryDto
{
    public int TotalNetworkMembers { get; set; }

    public int ActiveRiders { get; set; }
    public int ActiveMechanics { get; set; }
    public int ActiveAutoPartsStores { get; set; }
    public int ActiveCustomers { get; set; }

    public int CompletedOrders { get; set; }
    public int CompletedServiceRequests { get; set; }
    public int NetworkTransactions { get; set; }

    public decimal GrossNetworkActivity { get; set; }

    public decimal PendingRewards { get; set; }
    public decimal AvailableRewards { get; set; }
    public decimal PaidRewards { get; set; }
    public decimal LifetimeRewards { get; set; }

    public int CitiesConnected { get; set; }

    public string Currency { get; set; } = "MXN";
}