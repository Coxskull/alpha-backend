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

    public async Task<CommunityBuilderDashboardDto?>
        GetDashboardAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == userId,
                cancellationToken);

        if (user is null)
        {
            return null;
        }

        var directMembers = await _context.Users
            .AsNoTracking()
            .Where(x =>
                x.ReferredByUserId == userId)
            .ToListAsync(cancellationToken);

        var directMemberIds =
            directMembers
                .Select(x => x.Id)
                .ToList();

        var transactions =
            await _context.ReferralTransactions
                .AsNoTracking()
                .Where(x =>
                    x.ReferrerId == userId)
                .OrderByDescending(x =>
                    x.CreatedAt)
                .ToListAsync(cancellationToken);

        var frontendUrl =
            _configuration[
                "FrontendUrl"]
            ?? "http://localhost:3000";

        var referralCode =
            user.ReferralCode ??
            string.Empty;

        return new CommunityBuilderDashboardDto
        {
            UserId = user.Id,

            FullName =
                user.FullName ??
                string.Empty,

            ReferralCode =
                referralCode,

            ReferralLink =
                string.IsNullOrWhiteSpace(
                    referralCode)
                    ? string.Empty
                    : $"{frontendUrl.TrimEnd('/')}/register?ref={Uri.EscapeDataString(referralCode)}",

            DirectMembers =
                directMembers.Count,

            ActiveMembers =
                directMembers.Count,

            PendingRewards =
                transactions
                    .Where(x =>
                        x.Status == "pending")
                    .Sum(x => x.Amount),

            AvailableRewards =
                transactions
                    .Where(x =>
                        x.Status == "approved")
                    .Sum(x => x.Amount),

            PaidRewards =
                transactions
                    .Where(x =>
                        x.Status == "paid")
                    .Sum(x => x.Amount),

            Currency =
                transactions
                    .Select(x => x.Currency)
                    .FirstOrDefault()
                ?? "USD",

            Members =
                directMembers
                    .Select(member =>
                        new CommunityBuilderMemberDto
                        {
                            UserId =
                                member.Id,

                            FullName =
                                member.FullName ??
                                string.Empty,

                            PrimaryRole =
                                member.Role ??
                                string.Empty,

                            City =
                                member.City ??
                                string.Empty,

                            IsBusinessActive =
                                transactions.Any(x =>
                                    x.SourceUserId ==
                                    member.Id),

                            GeneratedRewards =
                                transactions
                                    .Where(x =>
                                        x.SourceUserId ==
                                        member.Id)
                                    .Sum(x =>
                                        x.Amount),

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
                            Id =
                                transaction.Id,

                            MemberName =
                                directMembers
                                    .FirstOrDefault(x =>
                                        x.Id ==
                                        transaction.SourceUserId)
                                    ?.FullName
                                ?? "Alpha member",

                            TransactionType =
                                transaction.TransactionType,

                            Description =
                                transaction.Description,

                            EligibleAmount =
                                transaction.EligibleAmount,

                            RewardAmount =
                                transaction.Amount,

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
                    .GroupBy(x =>
                        string.IsNullOrWhiteSpace(
                            x.City)
                            ? "Unspecified"
                            : x.City)
                    .Select(group =>
                        new CityNetworkDto
                        {
                            City =
                                group.Key,

                            TotalMembers =
                                group.Count(),

                            ActiveMembers =
                                group.Count(member =>
                                    transactions.Any(x =>
                                        x.SourceUserId ==
                                        member.Id)),

                            GeneratedRewards =
                                transactions
                                    .Where(transaction =>
                                        group.Any(member =>
                                            member.Id ==
                                            transaction.SourceUserId))
                                    .Sum(transaction =>
                                        transaction.Amount)
                        })
                    .OrderByDescending(x =>
                        x.TotalMembers)
                    .ToList()
        };
    }
}