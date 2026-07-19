using Alpha.API.Data;
using Alpha.API.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Alpha.API.Services;

public class ReferralCommissionService
{
    private readonly AppDbContext _context;

    public ReferralCommissionService(AppDbContext context)
    {
        _context = context;
    }

    public async Task GenerateOrderCommissionAsync(
        Guid sourceUserId,
        Guid orderId,
        Guid? paymentId,
        decimal grossAmount,
        string currency,
        string transactionType,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        if (grossAmount <= 0)
        {
            return;
        }

        var enabled = await GetBooleanSettingAsync(
            "referral_enabled",
            true,
            cancellationToken
        );

        if (!enabled)
        {
            return;
        }

        var maxLevels = await GetIntegerSettingAsync(
            "maximum_referral_levels",
            3,
            cancellationToken
        );

        var sourceUser = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                user => user.Id == sourceUserId &&
                        user.IsActive,
                cancellationToken
            );

        if (sourceUser == null)
        {
            return;
        }

        var currentReferrerId = sourceUser.ReferredByUserId;

        for (
            var level = 1;
            level <= maxLevels && currentReferrerId.HasValue;
            level++
        )
        {
            var referrer = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    user => user.Id == currentReferrerId.Value &&
                            user.IsActive,
                    cancellationToken
                );

            if (referrer == null)
            {
                break;
            }

            var rate = await GetDecimalSettingAsync(
                $"level_{level}_rate",
                0,
                cancellationToken
            );

            if (rate > 0)
            {
                var duplicateExists =
                    await _context.ReferralTransactions.AnyAsync(
                        transaction =>
                            transaction.BeneficiaryUserId == referrer.Id &&
                            transaction.SourceUserId == sourceUserId &&
                            transaction.OrderId == orderId &&
                            transaction.ReferralLevel == level &&
                            transaction.TransactionType == transactionType &&
                            transaction.Status != "reversed",
                        cancellationToken
                    );

                if (!duplicateExists)
                {
                    var commission = decimal.Round(
                        grossAmount * rate,
                        2,
                        MidpointRounding.AwayFromZero
                    );

                    _context.ReferralTransactions.Add(
                        new ReferralTransaction
                        {
                            Id = Guid.NewGuid(),
                            BeneficiaryUserId = referrer.Id,
                            SourceUserId = sourceUserId,
                            OrderId = orderId,
                            PaymentId = paymentId,
                            TransactionType = transactionType,
                            SourceRole = sourceUser.Role,
                            SourceDescription = description,
                            GrossAmount = grossAmount,
                            CommissionRate = rate,
                            CommissionAmount = commission,
                            Currency = currency.ToUpperInvariant(),
                            ReferralLevel = level,
                            Status = "pending",
                            CreatedAt = DateTime.UtcNow
                        }
                    );
                }
            }

            currentReferrerId = referrer.ReferredByUserId;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task ReleaseOrderCommissionsAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var transactions = await _context.ReferralTransactions
            .Where(transaction =>
                transaction.OrderId == orderId &&
                transaction.Status == "pending"
            )
            .ToListAsync(cancellationToken);

        foreach (var transaction in transactions)
        {
            transaction.Status = "available";
            transaction.AvailableAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task ReverseOrderCommissionsAsync(
        Guid orderId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var transactions = await _context.ReferralTransactions
            .Where(transaction =>
                transaction.OrderId == orderId &&
                (
                    transaction.Status == "pending" ||
                    transaction.Status == "available"
                )
            )
            .ToListAsync(cancellationToken);

        foreach (var transaction in transactions)
        {
            transaction.Status = "reversed";
            transaction.SourceDescription =
                $"{transaction.SourceDescription} Reversed: {reason}".Trim();
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<string?> GetSettingAsync(
        string key,
        CancellationToken cancellationToken)
    {
        return await _context.ReferralSettings
            .AsNoTracking()
            .Where(setting => setting.SettingKey == key)
            .Select(setting => setting.SettingValue)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<decimal> GetDecimalSettingAsync(
        string key,
        decimal fallback,
        CancellationToken cancellationToken)
    {
        var value = await GetSettingAsync(key, cancellationToken);

        return decimal.TryParse(value, out var result)
            ? result
            : fallback;
    }

    private async Task<int> GetIntegerSettingAsync(
        string key,
        int fallback,
        CancellationToken cancellationToken)
    {
        var value = await GetSettingAsync(key, cancellationToken);

        return int.TryParse(value, out var result)
            ? result
            : fallback;
    }

    private async Task<bool> GetBooleanSettingAsync(
        string key,
        bool fallback,
        CancellationToken cancellationToken)
    {
        var value = await GetSettingAsync(key, cancellationToken);

        return bool.TryParse(value, out var result)
            ? result
            : fallback;
    }

    public async Task GenerateFromBusinessEventAsync(
    ReferralBusinessEvent businessEvent,
    CancellationToken cancellationToken = default)
    {
        if (businessEvent.EligibleAmount <= 0)
            return;

        var sourceUser = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                user =>
                    user.Id == businessEvent.SourceUserId &&
                    user.IsActive,
                cancellationToken
            );

        if (sourceUser?.ReferredByUserId == null)
            return;

        var beneficiaryIsBuilder = await _context.UserRoles
            .AsNoTracking()
            .AnyAsync(
                role =>
                    role.UserId == sourceUser.ReferredByUserId &&
                    role.RoleKey == "community_builder" &&
                    role.Status == "active",
                cancellationToken
            );

        if (!beneficiaryIsBuilder)
            return;

        var duplicateExists = await _context.ReferralTransactions
            .AsNoTracking()
            .AnyAsync(
                transaction =>
                    transaction.BeneficiaryUserId ==
                        sourceUser.ReferredByUserId &&
                    transaction.SourceUserId ==
                        businessEvent.SourceUserId &&
                    transaction.EventKey ==
                        businessEvent.EventKey,
                cancellationToken
            );

        if (duplicateExists)
            return;

        var commissionRate = await ResolveRateAsync(
            businessEvent.TransactionType,
            cancellationToken
        );

        var commission = decimal.Round(
            businessEvent.EligibleAmount * commissionRate,
            2,
            MidpointRounding.AwayFromZero
        );

        if (commission <= 0)
            return;

        _context.ReferralTransactions.Add(
            new ReferralTransaction
            {
                Id = Guid.NewGuid(),
                BeneficiaryUserId =
                    sourceUser.ReferredByUserId.Value,
                SourceUserId = sourceUser.Id,
                OrderId = businessEvent.OrderId,
                ServiceRequestId =
                    businessEvent.ServiceRequestId,
                PaymentId = businessEvent.PaymentId,
                EventKey = businessEvent.EventKey,
                TransactionType =
                    businessEvent.TransactionType,
                SourceRole = businessEvent.SourceRole,
                SourceDescription =
                    businessEvent.Description,
                GrossAmount =
                    businessEvent.EligibleAmount,
                CommissionRate = commissionRate,
                CommissionAmount = commission,
                Currency = businessEvent.Currency,
                ReferralLevel = 1,
                Status = "pending",
                CreatedAt = DateTime.UtcNow
            }
        );

        await _context.SaveChangesAsync(cancellationToken);
    }
}