using Alpha.API.Data;
using Alpha.API.DTOs;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Alpha.API.Services;

public class ReferralNetworkService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public ReferralNetworkService(
        AppDbContext context,
        IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<ReferralDashboardDto> GetDashboardAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var currentUser = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                user => user.Id == currentUserId,
                cancellationToken
            );

        if (currentUser == null)
        {
            throw new KeyNotFoundException("User was not found.");
        }

        var networkMembers =
            await GetNetworkMembersAsync(
                currentUserId,
                cancellationToken
            );

        var directMembers = networkMembers
            .Where(member => member.Level == 1)
            .ToList();

        var transactions = await _context.ReferralTransactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.BeneficiaryUserId == currentUserId
            )
            .Include(transaction => transaction.SourceUser)
            .OrderByDescending(transaction => transaction.CreatedAt)
            .Take(100)
            .Select(transaction => new ReferralTransactionDto
            {
                Id = transaction.Id,
                SourceUserId = transaction.SourceUserId,
                SourceUserName = transaction.SourceUser.FullName,
                SourceRole =
                    transaction.SourceRole ??
                    transaction.SourceUser.Role,
                TransactionType = transaction.TransactionType,
                Description = transaction.SourceDescription,
                ReferralLevel = transaction.ReferralLevel,
                GrossAmount = transaction.GrossAmount,
                CommissionRate = transaction.CommissionRate,
                CommissionAmount = transaction.CommissionAmount,
                Currency = transaction.Currency,
                Status = transaction.Status,
                CreatedAt = transaction.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var allEarningsQuery = _context.ReferralTransactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.BeneficiaryUserId == currentUserId &&
                transaction.Status != "cancelled" &&
                transaction.Status != "reversed"
            );

        var pending = await allEarningsQuery
            .Where(transaction => transaction.Status == "pending")
            .SumAsync(
                transaction => (decimal?)transaction.CommissionAmount,
                cancellationToken
            ) ?? 0;

        var available = await allEarningsQuery
            .Where(transaction => transaction.Status == "available")
            .SumAsync(
                transaction => (decimal?)transaction.CommissionAmount,
                cancellationToken
            ) ?? 0;

        var paid = await allEarningsQuery
            .Where(transaction => transaction.Status == "paid")
            .SumAsync(
                transaction => (decimal?)transaction.CommissionAmount,
                cancellationToken
            ) ?? 0;

        var frontendUrl =
            _configuration["Frontend:BaseUrl"] ??
            "https://alpha-auto-mvp.vercel.app";

        var referralCode =
            currentUser.ReferralCode ??
            string.Empty;

        return new ReferralDashboardDto
        {
            ReferralCode = referralCode,
            ReferralLink =
                $"{frontendUrl.TrimEnd('/')}/register?ref={Uri.EscapeDataString(referralCode)}",

            Summary = new ReferralSummaryDto
            {
                DirectMembers = directMembers.Count(),
                TotalNetworkMembers = networkMembers.Count(),
                ActiveNetworkMembers =
                    networkMembers.Count(member => member.IsActive),
                NetworkTransactions = transactions.Count(),
                PendingEarnings = pending,
                AvailableEarnings = available,
                PaidEarnings = paid,
                LifetimeEarnings = pending + available + paid,
                Currency =
                    transactions.FirstOrDefault()?.Currency ??
                    "MXN"
            },

            DirectMembers = directMembers,
            NetworkMembers = networkMembers,
            RecentTransactions = transactions
        };
    }

    private async Task<List<ReferralMemberDto>> GetNetworkMembersAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var results = new List<ReferralMemberDto>();

        var connection = _context.Database.GetDbConnection();

        await _context.Database.OpenConnectionAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();

            command.CommandText = """
                WITH RECURSIVE referral_network AS (
                    SELECT
                        child."Id" AS user_id,
                        child."FullName" AS full_name,
                        child."Role" AS role,
                        child.referral_code,
                        child.referred_by_user_id,
                        child."CreatedAt" AS joined_at,
                        child."IsActive" AS is_active,
                        parent."FullName" AS referred_by_name,
                        1 AS level
                    FROM users child
                    LEFT JOIN users parent
                        ON parent."Id" = child.referred_by_user_id
                    WHERE child.referred_by_user_id = @current_user_id

                    UNION ALL

                    SELECT
                        child."Id",
                        child."FullName",
                        child."Role",
                        child.referral_code,
                        child.referred_by_user_id,
                        child."CreatedAt",
                        child."IsActive",
                        parent."FullName",
                        network.level + 1
                    FROM users child
                    INNER JOIN referral_network network
                        ON child.referred_by_user_id = network.user_id
                    LEFT JOIN users parent
                        ON parent."Id" = child.referred_by_user_id
                    WHERE network.level < 10
                )
                SELECT
                    network.user_id,
                    network.full_name,
                    network.role,
                    network.referral_code,
                    network.referred_by_user_id,
                    network.referred_by_name,
                    network.joined_at,
                    network.is_active,
                    network.level,
                    COUNT(transaction.id) AS transaction_count,
                    COALESCE(
                        SUM(transaction.gross_amount),
                        0
                    ) AS generated_volume,
                    COALESCE(
                        SUM(transaction.commission_amount),
                        0
                    ) AS generated_commission
                FROM referral_network network
                LEFT JOIN referral_transactions transaction
                    ON transaction.source_user_id = network.user_id
                    AND transaction.beneficiary_user_id = @current_user_id
                    AND transaction.status NOT IN (
                        'cancelled',
                        'reversed'
                    )
                GROUP BY
                    network.user_id,
                    network.full_name,
                    network.role,
                    network.referral_code,
                    network.referred_by_user_id,
                    network.referred_by_name,
                    network.joined_at,
                    network.is_active,
                    network.level
                ORDER BY
                    network.level,
                    network.joined_at DESC;
                """;

            var parameter = command.CreateParameter();
            parameter.ParameterName = "@current_user_id";
            parameter.Value = userId;
            command.Parameters.Add(parameter);

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new ReferralMemberDto
                {
                    UserId = reader.GetGuid(0),
                    FullName = reader.GetString(1),
                    Role = reader.GetString(2),
                    ReferralCode =
                        reader.IsDBNull(3)
                            ? null
                            : reader.GetString(3),
                    ReferredByUserId =
                        reader.IsDBNull(4)
                            ? null
                            : reader.GetGuid(4),
                    ReferredByName =
                        reader.IsDBNull(5)
                            ? null
                            : reader.GetString(5),
                    JoinedAt = reader.GetDateTime(6),
                    IsActive = reader.GetBoolean(7),
                    Level = reader.GetInt32(8),
                    TransactionCount =
                        Convert.ToInt32(reader.GetInt64(9)),
                    GeneratedVolume = reader.GetDecimal(10),
                    GeneratedCommission = reader.GetDecimal(11)
                });
            }
        }
        finally
        {
            await _context.Database.CloseConnectionAsync();
        }

        return results;
    }
}