using Alpha.API.Data;
using Alpha.API.DTOs;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Alpha.API.Services;

public class CommunityBuilderDashboardService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public CommunityBuilderDashboardService(
        AppDbContext context,
        IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<CommunityBuilderDashboardDto?> GetDashboardAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == userId,
                cancellationToken);

        if (user == null)
            return null;

        var directMembers = await _context.Users
            .AsNoTracking()
            .Where(x => x.ReferredByUserId == userId)
            .ToListAsync(cancellationToken);

        var transactions = await _context.ReferralTransactions
            .AsNoTracking()
            .Where(x => x.BeneficiaryUserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var frontendUrl =
            _configuration["FrontendUrl"] ??
            "http://localhost:3000";

        var referralCode = user.ReferralCode ?? string.Empty;

        return new CommunityBuilderDashboardDto
        {
            UserId = user.Id,

            FullName = user.FullName,

            ReferralCode = referralCode,

            ReferralLink = string.IsNullOrWhiteSpace(referralCode)
                ? string.Empty
                : $"{frontendUrl.TrimEnd('/')}/register?ref={Uri.EscapeDataString(referralCode)}",

            DirectMembers = directMembers.Count,

            ActiveMembers =
                directMembers.Count(member =>
                    transactions.Any(transaction =>
                        transaction.SourceUserId == member.Id)),

            PendingRewards =
                transactions
                    .Where(x => x.Status == "pending")
                    .Sum(x => x.CommissionAmount),

            AvailableRewards =
                transactions
                    .Where(x =>
                        x.Status == "available" ||
                        x.Status == "approved")
                    .Sum(x => x.CommissionAmount),

            PaidRewards =
                transactions
                    .Where(x => x.Status == "paid")
                    .Sum(x => x.CommissionAmount),

            Currency =
                transactions
                    .Select(x => x.Currency)
                    .FirstOrDefault() ??
                "USD",

            Members =
                directMembers
                    .Select(member =>
                        new CommunityBuilderMemberDto
                        {
                            UserId = member.Id,

                            FullName = member.FullName,

                            PrimaryRole = member.Role,

                            // City will come from entrepreneur_profiles later
                            City = "Unspecified",

                            IsBusinessActive =
                                transactions.Any(x =>
                                    x.SourceUserId == member.Id),

                            GeneratedRewards =
                                transactions
                                    .Where(x =>
                                        x.SourceUserId == member.Id)
                                    .Sum(x =>
                                        x.CommissionAmount),

                            JoinedAt =
                                member.CreatedAt
                        })
                    .ToList(),

            RecentActivities =
                transactions
                    .Take(20)
                    .Select(transaction =>
                        new NetworkActivityDto
                        {
                            Id = transaction.Id,

                            MemberName =
                                directMembers
                                    .FirstOrDefault(member =>
                                        member.Id ==
                                        transaction.SourceUserId)
                                    ?.FullName ??
                                "Alpha member",

                            TransactionType =
                                transaction.TransactionType,

                            Description =
                                transaction.Description ??
                                transaction.SourceDescription ??
                                string.Empty,

                            EligibleAmount =
                                transaction.EligibleAmount,

                            RewardAmount =
                                transaction.CommissionAmount,

                            Currency =
                                transaction.Currency,

                            Status =
                                transaction.Status,

                            CreatedAt =
                                transaction.CreatedAt
                        })
                    .ToList(),

            Cities =
                directMembers
                    .GroupBy(_ => "Unspecified")
                    .Select(group =>
                        new CityNetworkDto
                        {
                            City = group.Key,

                            TotalMembers = group.Count(),

                            ActiveMembers =
                                group.Count(member =>
                                    transactions.Any(transaction =>
                                        transaction.SourceUserId ==
                                        member.Id)),

                            GeneratedRewards =
                                transactions
                                    .Where(transaction =>
                                        group.Any(member =>
                                            member.Id ==
                                            transaction.SourceUserId))
                                    .Sum(transaction =>
                                        transaction.CommissionAmount)
                        })
                    .ToList()
        };
    }
}