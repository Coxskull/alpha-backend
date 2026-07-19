using Alpha.API.Data;
using Alpha.API.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Alpha.API.Services;

public class ReferralCommissionService
{
    private readonly AppDbContext _context;

    public ReferralCommissionService(
        AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Generates multi-level referral commissions for an order.
    /// This uses the configured level_1_rate, level_2_rate,
    /// level_3_rate, and maximum_referral_levels settings.
    /// </summary>
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
        if (sourceUserId == Guid.Empty ||
            orderId == Guid.Empty ||
            grossAmount <= 0)
        {
            return;
        }

        var normalizedCurrency =
            NormalizeCurrency(currency);

        var normalizedTransactionType =
            NormalizeValue(transactionType);

        if (string.IsNullOrWhiteSpace(
            normalizedTransactionType))
        {
            return;
        }

        var enabled = await GetBooleanSettingAsync(
            "referral_enabled",
            true,
            cancellationToken);

        if (!enabled)
        {
            return;
        }

        var maxLevels = await GetIntegerSettingAsync(
            "maximum_referral_levels",
            3,
            cancellationToken);

        if (maxLevels <= 0)
        {
            return;
        }

        var sourceUser = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                user =>
                    user.Id == sourceUserId &&
                    user.IsActive,
                cancellationToken);

        if (sourceUser is null)
        {
            return;
        }

        var currentReferrerId =
            sourceUser.ReferredByUserId;

        for (
            var level = 1;
            level <= maxLevels &&
            currentReferrerId.HasValue;
            level++)
        {
            var referrer = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    user =>
                        user.Id ==
                        currentReferrerId.Value &&
                        user.IsActive,
                    cancellationToken);

            if (referrer is null)
            {
                break;
            }

            var rate = await GetDecimalSettingAsync(
                $"level_{level}_rate",
                0m,
                cancellationToken);

            if (rate > 0m)
            {
                var duplicateExists =
                    await _context
                        .ReferralTransactions
                        .AsNoTracking()
                        .AnyAsync(
                            transaction =>
                                transaction
                                    .BeneficiaryUserId ==
                                referrer.Id &&
                                transaction.SourceUserId ==
                                sourceUserId &&
                                transaction.OrderId ==
                                orderId &&
                                transaction.ReferralLevel ==
                                level &&
                                transaction.TransactionType ==
                                normalizedTransactionType &&
                                transaction.Status !=
                                "reversed",
                            cancellationToken);

                if (!duplicateExists)
                {
                    var commission =
                        decimal.Round(
                            grossAmount * rate,
                            2,
                            MidpointRounding
                                .AwayFromZero);

                    if (commission > 0m)
                    {
                        _context
                            .ReferralTransactions
                            .Add(
                                new ReferralTransaction
                                {
                                    Id =
                                        Guid.NewGuid(),

                                    BeneficiaryUserId =
                                        referrer.Id,

                                    SourceUserId =
                                        sourceUserId,

                                    OrderId =
                                        orderId,

                                    PaymentId =
                                        paymentId,

                                    EventKey =
                                        BuildOrderEventKey(
                                            orderId,
                                            normalizedTransactionType,
                                            level),

                                    TransactionType =
                                        normalizedTransactionType,

                                    SourceRole =
                                        NormalizeValue(
                                            sourceUser.Role),

                                    SourceDescription =
                                        description,

                                    GrossAmount =
                                        grossAmount,

                                    CommissionRate =
                                        rate,

                                    CommissionAmount =
                                        commission,

                                    Currency =
                                        normalizedCurrency,

                                    ReferralLevel =
                                        level,

                                    Status =
                                        "pending",

                                    CreatedAt =
                                        DateTime.UtcNow
                                });
                    }
                }
            }

            currentReferrerId =
                referrer.ReferredByUserId;
        }

        await _context.SaveChangesAsync(
            cancellationToken);
    }

    /// <summary>
    /// Generates a direct Community Builder reward from
    /// a completed eligible business event.
    /// </summary>
    public async Task GenerateFromBusinessEventAsync(
        ReferralBusinessEvent businessEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            businessEvent);

        if (businessEvent.SourceUserId ==
                Guid.Empty ||
            businessEvent.EligibleAmount <= 0m)
        {
            return;
        }

        var eventKey =
            businessEvent.EventKey?.Trim();

        if (string.IsNullOrWhiteSpace(
            eventKey))
        {
            throw new ArgumentException(
                "A referral business event must have an EventKey.",
                nameof(businessEvent));
        }

        var transactionType =
            NormalizeValue(
                businessEvent.TransactionType);

        if (string.IsNullOrWhiteSpace(
            transactionType))
        {
            throw new ArgumentException(
                "A referral business event must have a TransactionType.",
                nameof(businessEvent));
        }

        var sourceRole =
            NormalizeValue(
                businessEvent.SourceRole);

        var currency =
            NormalizeCurrency(
                businessEvent.Currency);

        var referralEnabled =
            await GetBooleanSettingAsync(
                "referral_enabled",
                true,
                cancellationToken);

        if (!referralEnabled)
        {
            return;
        }

        var sourceUser = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                user =>
                    user.Id ==
                    businessEvent.SourceUserId &&
                    user.IsActive,
                cancellationToken);

        if (sourceUser?.ReferredByUserId is null)
        {
            return;
        }

        var beneficiaryUserId =
            sourceUser.ReferredByUserId.Value;

        var beneficiary = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                user =>
                    user.Id ==
                    beneficiaryUserId &&
                    user.IsActive,
                cancellationToken);

        if (beneficiary is null)
        {
            return;
        }

        var beneficiaryIsBuilder =
            await _context.UserRoles
                .AsNoTracking()
                .AnyAsync(
                    role =>
                        role.UserId ==
                        beneficiaryUserId &&
                        role.RoleKey ==
                        "community_builder" &&
                        role.Status ==
                        "active",
                    cancellationToken);

        if (!beneficiaryIsBuilder)
        {
            return;
        }

        var duplicateExists =
            await _context
                .ReferralTransactions
                .AsNoTracking()
                .AnyAsync(
                    transaction =>
                        transaction.EventKey ==
                            eventKey &&
                        transaction
                            .BeneficiaryUserId ==
                            beneficiaryUserId &&
                        transaction.Status !=
                            "reversed",
                    cancellationToken);

        if (duplicateExists)
        {
            return;
        }

        var commissionRate =
            await ResolveRateAsync(
                transactionType,
                sourceRole,
                cancellationToken);

        if (commissionRate <= 0m)
        {
            return;
        }

        var commission =
            decimal.Round(
                businessEvent.EligibleAmount *
                commissionRate,
                2,
                MidpointRounding.AwayFromZero);

        if (commission <= 0m)
        {
            return;
        }

        _context.ReferralTransactions.Add(
            new ReferralTransaction
            {
                Id =
                    Guid.NewGuid(),

                BeneficiaryUserId =
                    beneficiaryUserId,

                SourceUserId =
                    sourceUser.Id,

                OrderId =
                    businessEvent.OrderId,

                ServiceRequestId =
                    businessEvent.ServiceRequestId,

                PaymentId =
                    businessEvent.PaymentId,

                EventKey =
                    eventKey,

                TransactionType =
                    transactionType,

                SourceRole =
                    string.IsNullOrWhiteSpace(
                        sourceRole)
                        ? NormalizeValue(
                            sourceUser.Role)
                        : sourceRole,

                SourceDescription =
                    businessEvent.Description,

                GrossAmount =
                    businessEvent.EligibleAmount,

                CommissionRate =
                    commissionRate,

                CommissionAmount =
                    commission,

                Currency =
                    currency,

                ReferralLevel =
                    1,

                Status =
                    "pending",

                CreatedAt =
                    DateTime.UtcNow
            });

        await _context.SaveChangesAsync(
            cancellationToken);
    }

    /// <summary>
    /// Marks pending referral commissions for an order
    /// as available.
    /// </summary>
    public async Task ReleaseOrderCommissionsAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        if (orderId == Guid.Empty)
        {
            return;
        }

        var transactions =
            await _context
                .ReferralTransactions
                .Where(
                    transaction =>
                        transaction.OrderId ==
                            orderId &&
                        transaction.Status ==
                            "pending")
                .ToListAsync(
                    cancellationToken);

        if (transactions.Count == 0)
        {
            return;
        }

        var availableAt =
            DateTime.UtcNow;

        foreach (var transaction in transactions)
        {
            transaction.Status =
                "available";

            transaction.AvailableAt =
                availableAt;
        }

        await _context.SaveChangesAsync(
            cancellationToken);
    }

    /// <summary>
    /// Reverses pending or available referral commissions
    /// connected to an order.
    /// </summary>
    public async Task ReverseOrderCommissionsAsync(
        Guid orderId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (orderId == Guid.Empty)
        {
            return;
        }

        var transactions =
            await _context
                .ReferralTransactions
                .Where(
                    transaction =>
                        transaction.OrderId ==
                            orderId &&
                        (
                            transaction.Status ==
                                "pending" ||
                            transaction.Status ==
                                "available"
                        ))
                .ToListAsync(
                    cancellationToken);

        if (transactions.Count == 0)
        {
            return;
        }

        var normalizedReason =
            string.IsNullOrWhiteSpace(reason)
                ? "Business transaction reversed."
                : reason.Trim();

        foreach (var transaction in transactions)
        {
            transaction.Status =
                "reversed";

            var existingDescription =
                transaction.SourceDescription?
                    .Trim();

            transaction.SourceDescription =
                string.IsNullOrWhiteSpace(
                    existingDescription)
                    ? $"Reversed: {normalizedReason}"
                    : $"{existingDescription} Reversed: {normalizedReason}";
        }

        await _context.SaveChangesAsync(
            cancellationToken);
    }

    /// <summary>
    /// Resolves the commission rate for a business event.
    ///
    /// Resolution order:
    /// 1. transaction_type + source_role setting
    /// 2. transaction_type setting
    /// 3. default_business_event_rate
    /// 4. level_1_rate
    /// </summary>
    private async Task<decimal> ResolveRateAsync(
        string transactionType,
        string sourceRole,
        CancellationToken cancellationToken)
    {
        var normalizedTransactionType =
            NormalizeValue(transactionType);

        var normalizedSourceRole =
            NormalizeValue(sourceRole);

        if (!string.IsNullOrWhiteSpace(
                normalizedTransactionType) &&
            !string.IsNullOrWhiteSpace(
                normalizedSourceRole))
        {
            var roleSpecificRate =
                await GetOptionalDecimalSettingAsync(
                    $"{normalizedTransactionType}_{normalizedSourceRole}_rate",
                    cancellationToken);

            if (roleSpecificRate.HasValue)
            {
                return NormalizeRate(
                    roleSpecificRate.Value);
            }
        }

        if (!string.IsNullOrWhiteSpace(
            normalizedTransactionType))
        {
            var transactionRate =
                await GetOptionalDecimalSettingAsync(
                    $"{normalizedTransactionType}_rate",
                    cancellationToken);

            if (transactionRate.HasValue)
            {
                return NormalizeRate(
                    transactionRate.Value);
            }
        }

        var defaultBusinessEventRate =
            await GetOptionalDecimalSettingAsync(
                "default_business_event_rate",
                cancellationToken);

        if (defaultBusinessEventRate.HasValue)
        {
            return NormalizeRate(
                defaultBusinessEventRate.Value);
        }

        var levelOneRate =
            await GetDecimalSettingAsync(
                "level_1_rate",
                0m,
                cancellationToken);

        return NormalizeRate(
            levelOneRate);
    }

    private async Task<string?> GetSettingAsync(
        string key,
        CancellationToken cancellationToken)
    {
        return await _context.ReferralSettings
            .AsNoTracking()
            .Where(
                setting =>
                    setting.SettingKey == key)
            .Select(
                setting =>
                    setting.SettingValue)
            .FirstOrDefaultAsync(
                cancellationToken);
    }

    private async Task<decimal?>
        GetOptionalDecimalSettingAsync(
            string key,
            CancellationToken cancellationToken)
    {
        var value = await GetSettingAsync(
            key,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (decimal.TryParse(
            value,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var invariantResult))
        {
            return invariantResult;
        }

        if (decimal.TryParse(
            value,
            out var localResult))
        {
            return localResult;
        }

        return null;
    }

    private async Task<decimal>
        GetDecimalSettingAsync(
            string key,
            decimal fallback,
            CancellationToken cancellationToken)
    {
        var value =
            await GetOptionalDecimalSettingAsync(
                key,
                cancellationToken);

        return value ?? fallback;
    }

    private async Task<int>
        GetIntegerSettingAsync(
            string key,
            int fallback,
            CancellationToken cancellationToken)
    {
        var value = await GetSettingAsync(
            key,
            cancellationToken);

        return int.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var result)
            ? result
            : fallback;
    }

    private async Task<bool>
        GetBooleanSettingAsync(
            string key,
            bool fallback,
            CancellationToken cancellationToken)
    {
        var value = await GetSettingAsync(
            key,
            cancellationToken);

        if (bool.TryParse(
            value,
            out var booleanResult))
        {
            return booleanResult;
        }

        if (int.TryParse(
                value,
                out var numericResult))
        {
            return numericResult != 0;
        }

        return fallback;
    }

    private static decimal NormalizeRate(
        decimal rate)
    {
        if (rate <= 0m)
        {
            return 0m;
        }

        // Allows both formats:
        // 0.02 = 2%
        // 2 = 2%
        if (rate > 1m)
        {
            rate /= 100m;
        }

        return rate > 1m
            ? 1m
            : rate;
    }

    private static string NormalizeCurrency(
        string? currency)
    {
        return string.IsNullOrWhiteSpace(
            currency)
            ? "USD"
            : currency.Trim()
                .ToUpperInvariant();
    }

    private static string NormalizeValue(
        string? value)
    {
        return string.IsNullOrWhiteSpace(
            value)
            ? string.Empty
            : value.Trim()
                .ToLowerInvariant()
                .Replace(' ', '_')
                .Replace('-', '_');
    }

    private static string BuildOrderEventKey(
        Guid orderId,
        string transactionType,
        int level)
    {
        return
            $"order:{orderId}:{transactionType}:level:{level}";
    }

    public Task<ReferralTransaction?>
    ProcessCustomerPurchaseAsync(
        Guid customerUserId,
        Guid orderId,
        Guid? paymentId,
        decimal eligibleAmount,
        string currency,
        CancellationToken cancellationToken = default)
    {
        return CreateCommissionAsync(
            new ReferralBusinessEvent
            {
                EventKey =
                    $"order:{orderId}:customer-purchase",

                TransactionType =
                    "customer_purchase",

                SourceUserId =
                    customerUserId,

                SourceRole =
                    "customer",

                OrderId =
                    orderId,

                PaymentId =
                    paymentId,

                EligibleAmount =
                    eligibleAmount,

                Currency =
                    currency,

                Description =
                    "Reward generated from an eligible customer purchase."
            },
            cancellationToken);
    }
    public Task<ReferralTransaction?>
    ProcessCustomerPurchaseAsync(
        Guid customerUserId,
        Guid orderId,
        Guid? paymentId,
        decimal eligibleAmount,
        string currency,
        CancellationToken cancellationToken = default)
    {
        return CreateCommissionAsync(
            new ReferralBusinessEvent
            {
                EventKey =
                    $"order:{orderId}:customer-purchase",

                TransactionType =
                    "customer_purchase",

                SourceUserId =
                    customerUserId,

                SourceRole =
                    "customer",

                OrderId =
                    orderId,

                PaymentId =
                    paymentId,

                EligibleAmount =
                    eligibleAmount,

                Currency =
                    currency,

                Description =
                    "Reward generated from an eligible customer purchase."
            },
            cancellationToken);
    }
    public Task<ReferralTransaction?>
    ProcessDriverDeliveryAsync(
        Guid driverUserId,
        Guid orderId,
        decimal eligibleAmount,
        string currency,
        CancellationToken cancellationToken = default)
    {
        return CreateCommissionAsync(
            new ReferralBusinessEvent
            {
                EventKey =
                    $"order:{orderId}:driver-delivery",

                TransactionType =
                    "driver_delivery",

                SourceUserId =
                    driverUserId,

                SourceRole =
                    "driver",

                OrderId =
                    orderId,

                EligibleAmount =
                    eligibleAmount,

                Currency =
                    currency,

                Description =
                    "Reward generated from a completed delivery."
            },
            cancellationToken);
    }

    public Task<ReferralTransaction?>
    ProcessSupplierFulfillmentAsync(
        Guid supplierUserId,
        Guid orderId,
        decimal eligibleAmount,
        string currency,
        CancellationToken cancellationToken = default)
    {
        return CreateCommissionAsync(
            new ReferralBusinessEvent
            {
                EventKey =
                    $"order:{orderId}:supplier-fulfillment",

                TransactionType =
                    "supplier_fulfillment",

                SourceUserId =
                    supplierUserId,

                SourceRole =
                    "supplier",

                OrderId =
                    orderId,

                EligibleAmount =
                    eligibleAmount,

                Currency =
                    currency,

                Description =
                    "Reward generated from a fulfilled parts order."
            },
            cancellationToken);
    }

    public Task<ReferralTransaction?>
    ProcessMechanicServiceAsync(
        Guid mechanicUserId,
        Guid serviceRequestId,
        decimal eligibleAmount,
        string currency,
        CancellationToken cancellationToken = default)
    {
        return CreateCommissionAsync(
            new ReferralBusinessEvent
            {
                EventKey =
                    $"service:{serviceRequestId}:mechanic-completed",

                TransactionType =
                    "mechanic_service",

                SourceUserId =
                    mechanicUserId,

                SourceRole =
                    "mechanic",

                ServiceRequestId =
                    serviceRequestId,

                EligibleAmount =
                    eligibleAmount,

                Currency =
                    currency,

                Description =
                    "Reward generated from a completed mechanic service."
            },
            cancellationToken);
    }
}