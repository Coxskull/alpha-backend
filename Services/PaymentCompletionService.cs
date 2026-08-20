using Alpha.API.Constants;
using Alpha.API.Data;
using Alpha.API.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Alpha.API.Services.Entrepreneur;
using Alpha.API.Models.Entrepreneur;
namespace Alpha.API.Services;

public class PaymentCompletionService
{
    private readonly AppDbContext _context;
    private readonly SettlementService _settlements;
    private readonly ReferralCommissionService _referralCommissionService;
    private readonly DirectTransactionCostService _entrepreneurDirectTransactionCostService;
    private readonly EntrepreneurCommissionService _entrepreneurCommissionService;

    public PaymentCompletionService(
     AppDbContext context,
     SettlementService settlements,
     ReferralCommissionService referralCommissionService,
     DirectTransactionCostService entrepreneurDirectTransactionCostService,
     EntrepreneurCommissionService entrepreneurCommissionService)
    {
        _context = context;
        _settlements = settlements;
        _referralCommissionService =
            referralCommissionService;

        _entrepreneurDirectTransactionCostService =
            entrepreneurDirectTransactionCostService;

        _entrepreneurCommissionService =
            entrepreneurCommissionService;
    }

    public async Task CompleteOrderPaymentAsync(
        Guid orderId,
        string gateway,
        string paymentMethod,
        string transactionReference,
        string? gatewayPaymentId,
        string rawGatewayResponse,
        decimal? gatewayFee,
        CancellationToken cancellationToken = default)
    {
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException(
                "A valid order ID is required.",
                nameof(orderId));
        }

        if (string.IsNullOrWhiteSpace(gateway))
        {
            throw new ArgumentException(
                "Payment gateway is required.",
                nameof(gateway));
        }

        if (string.IsNullOrWhiteSpace(paymentMethod))
        {
            throw new ArgumentException(
                "Payment method is required.",
                nameof(paymentMethod));
        }

        if (string.IsNullOrWhiteSpace(transactionReference))
        {
            throw new ArgumentException(
                "Transaction reference is required.",
                nameof(transactionReference));
        }

        var normalizedGateway =
            gateway.Trim().ToLowerInvariant();

        var normalizedPaymentMethod =
            paymentMethod.Trim().ToLowerInvariant();

        var order = await _context.Orders
            .FirstOrDefaultAsync(
                x => x.Id == orderId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "Order not found.");

        var payment = await _context.Payments
            .FirstOrDefaultAsync(
                x => x.OrderId == orderId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "Payment record not found.");

        var financial = await _context.OrderFinancials
            .FirstOrDefaultAsync(
                x => x.OrderId == orderId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "Financial record not found.");

        /*
         * Payment completion is idempotent.
         *
         * Payment gateways can send the same webhook more than once.
         */
        if (string.Equals(
                payment.PaymentStatus,
                "paid",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!order.CustomerId.HasValue)
        {
            throw new InvalidOperationException(
                "Order is not connected to a registered customer.");
        }

        if (payment.Amount != financial.TotalAmount)
        {
            throw new InvalidOperationException(
                $"Payment amount {payment.Amount} does not match " +
                $"the financial total {financial.TotalAmount}.");
        }

        if (!string.Equals(
                payment.Currency,
                financial.Currency,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Payment and financial currencies do not match.");
        }

        if (gatewayFee.HasValue && gatewayFee.Value < 0)
        {
            throw new InvalidOperationException(
                "Gateway fee cannot be negative.");
        }

        var now = DateTime.UtcNow;

        var storedGatewayResponse =
            NormalizeGatewayResponse(rawGatewayResponse);

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                cancellationToken);

        try
        {
            /*
             * ---------------------------------------------------------
             * PAYMENT
             * ---------------------------------------------------------
             */

            payment.PaymentStatus = "paid";

            payment.PaymentMethod =
                normalizedPaymentMethod;

            payment.PaymentGateway =
                normalizedGateway;

            payment.TransactionReference =
                transactionReference.Trim();

            payment.GatewayPaymentId =
                string.IsNullOrWhiteSpace(gatewayPaymentId)
                    ? null
                    : gatewayPaymentId.Trim();

            payment.PaidAt = now;

            payment.GatewayFee =
                gatewayFee ?? 0m;

            /*
             * Use the normalized JSON response where possible.
             */
            if (!string.IsNullOrWhiteSpace(storedGatewayResponse))
            {
                try
                {
                    payment.GatewayResponse =
                        JsonDocument.Parse(
                            storedGatewayResponse);
                }
                catch (JsonException)
                {
                    payment.GatewayResponse = null;
                }
            }
            else
            {
                payment.GatewayResponse = null;
            }

            payment.FailureReason = null;


            /*
             * ---------------------------------------------------------
             * FINANCIAL
             * ---------------------------------------------------------
             */

            financial.CustomerPaid =
                financial.TotalAmount;

            financial.ProcessingFee =
                gatewayFee ?? 0m;


            /*
             * ---------------------------------------------------------
             * DIRECT PAYMENT TRANSACTION COST
             * ---------------------------------------------------------
             */

            if (gatewayFee.HasValue &&
                gatewayFee.Value > 0m)
            {
                _context
                    .EntrepreneurTransactionCosts
                    .Add(
                        new EntrepreneurTransactionCost
                        {
                            Id = Guid.NewGuid(),

                            OrderId =
                                order.Id,

                            PaymentId =
                                payment.Id,

                            CostType =
                                "payment_processing_fee",

                            Amount =
                                gatewayFee.Value,

                            Currency =
                                payment.Currency,

                            Description =
                                $"{normalizedGateway} processing fee",

                            CreatedAt =
                                now
                        });
            }


            /*
             * ---------------------------------------------------------
             * ORDER STATUS
             * ---------------------------------------------------------
             */

            financial.FinancialStatus =
                "paid_pending_dispatch";

            financial.PayoutStatus =
                "not_ready";

            order.Status =
                OrderStatuses.WaitingForSupplier;

            order.UpdatedAt =
                now;


            /*
             * ---------------------------------------------------------
             * STATUS HISTORY
             * ---------------------------------------------------------
             */

            _context.StatusHistory.AddRange(
                new StatusHistory
                {
                    Id = Guid.NewGuid(),

                    OrderId =
                        order.Id,

                    Status =
                        OrderStatuses.PaymentPaid,

                    Notes =
                        $"{normalizedGateway} payment confirmed.",

                    CreatedAt =
                        now
                },

                new StatusHistory
                {
                    Id = Guid.NewGuid(),

                    OrderId =
                        order.Id,

                    Status =
                        OrderStatuses.WaitingForSupplier,

                    Notes =
                        "Order moved to supplier assignment.",

                    CreatedAt =
                        now
                });


            /*
             * ---------------------------------------------------------
             * AUDIT LOG
             * ---------------------------------------------------------
             */

            _context.AuditLogs.Add(
                new AuditLog
                {
                    Id = Guid.NewGuid(),

                    OrderId =
                        order.Id,

                    Action =
                        $"{normalizedGateway} Payment Confirmed",

                    PerformedBy =
                        normalizedGateway,

                    CreatedAt =
                        now
                });


            /*
             * ---------------------------------------------------------
             * SAVE PAYMENT CHANGES
             * ---------------------------------------------------------
             */

            await _context.SaveChangesAsync(
                cancellationToken);


            /*
             * ---------------------------------------------------------
             * SETTLEMENT
             * ---------------------------------------------------------
             */

            await _settlements
                .CreateOrUpdateSettlementAfterPayment(
                    order.Id);


            /*
             * ---------------------------------------------------------
             * MARKETPLACE REFERRAL COMMISSION
             *
             * This remains separate from the Entrepreneur Network.
             * ---------------------------------------------------------
             */

            await _referralCommissionService
                .GenerateOrderCommissionAsync(
                    sourceUserId:
                        order.CustomerId.Value,

                    orderId:
                        order.Id,

                    paymentId:
                        payment.Id,

                    grossAmount:
                        payment.Amount,

                    currency:
                        payment.Currency,

                    transactionType:
                        "customer_order",

                    description:
                        $"Completed payment for order " +
                        $"{order.OrderNumber}",

                    cancellationToken:
                        cancellationToken);


            /*
  * ---------------------------------------------------------
  * ENTREPRENEUR DIRECT TRANSACTION COSTS
  * ---------------------------------------------------------
  */

            var directTransactionCosts =
                await _entrepreneurDirectTransactionCostService
                    .CalculateAsync(
                        order.Id,
                        cancellationToken);

            financial.DirectTransactionCosts =
                directTransactionCosts;

            /*
             * ---------------------------------------------------------
             * ENTREPRENEUR COMMISSION
             * ---------------------------------------------------------
             *
             * This is the critical connection between the
             * real Alpha payment workflow and the Entrepreneur Network.
             */

            await _entrepreneurCommissionService
                .GenerateForOrderAsync(
                    order.Id,
                    cancellationToken);

            /*
             * ---------------------------------------------------------
             * LOAD THE RESULTING ENTREPRENEUR EARNING
             * ---------------------------------------------------------
             */

            var entrepreneurEarning =
                await _context
                    .EntrepreneurEarnings
                    .FirstOrDefaultAsync(
                        x =>
                            x.OrderId == order.Id,
                        cancellationToken);

            /*
             * ---------------------------------------------------------
             * ELIGIBLE NET PLATFORM REVENUE
             * ---------------------------------------------------------
             */

            financial.AlphaEligibleNetPlatformRevenue =
                Math.Max(
                    0m,
                    financial.AlphaGrossPlatformCommission -
                    directTransactionCosts);

            /*
             * ---------------------------------------------------------
             * ENTREPRENEUR COMMISSION
             * ---------------------------------------------------------
             */

            financial.EntrepreneurCommission =
                entrepreneurEarning?
                    .EntrepreneurEarningsAmount
                    ?? 0m;

            /*
             * ---------------------------------------------------------
             * ALPHA RETAINED REVENUE
             * ---------------------------------------------------------
             */

            financial.AlphaRetainedRevenue =
                Math.Max(
                    0m,
                    financial.AlphaEligibleNetPlatformRevenue -
                    financial.EntrepreneurCommission);


            financial.CustomerPaid =
    financial.TotalAmount;

            financial.SupplierNetPayable =
                financial.SupplierEarning;

            financial.AlphaGrossPlatformCommission =
                financial.AlphaGrossPartsCommission
                +
                financial.AlphaGrossMechanicCommission
                +
                financial.AlphaGrossDeliveryCommission;

            financial.DirectTransactionCosts =
                directTransactionCosts;

            financial.AlphaEligibleNetPlatformRevenue =
                Math.Max(
                    0m,
                    financial.AlphaGrossPlatformCommission -
                    directTransactionCosts);

            financial.EntrepreneurCommission =
                entrepreneurEarning?
                    .EntrepreneurEarningsAmount
                    ?? 0m;

            financial.AlphaRetainedRevenue =
                Math.Max(
                    0m,
                    financial.AlphaEligibleNetPlatformRevenue -
                    financial.EntrepreneurCommission);
            /*
             * Save the Entrepreneur-related financial values.
             */
            await _context.SaveChangesAsync(
                cancellationToken);


            /*
             * ---------------------------------------------------------
             * COMMIT
             * ---------------------------------------------------------
             */

            await transaction.CommitAsync(
                cancellationToken);


        }
        catch
        {
            await transaction.RollbackAsync(
                cancellationToken);

            throw;
        }
    }


    private static string? NormalizeGatewayResponse(
        string? rawGatewayResponse)
    {
        if (string.IsNullOrWhiteSpace(rawGatewayResponse))
        {
            return null;
        }

        var trimmedResponse =
            rawGatewayResponse.Trim();

        try
        {
            using var document =
                JsonDocument.Parse(
                    trimmedResponse);

            return document.RootElement
                .GetRawText();
        }
        catch (JsonException)
        {
            /*
             * Some gateways may return plain text or HTML.
             * Preserve the response for troubleshooting.
             */
            return trimmedResponse;
        }
    }
}