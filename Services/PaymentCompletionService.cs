using Alpha.API.Constants;
using Alpha.API.Data;
using Alpha.API.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Alpha.API.Services;

public class PaymentCompletionService
{
    private readonly AppDbContext _context;
    private readonly SettlementService _settlements;
    private readonly ReferralCommissionService _referralCommissionService;

    public PaymentCompletionService(
        AppDbContext context,
        SettlementService settlements,
        ReferralCommissionService referralCommissionService)
    {
        _context = context;
        _settlements = settlements;
        _referralCommissionService = referralCommissionService;
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

        var normalizedGateway = gateway.Trim().ToLowerInvariant();
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
         * Makes the payment completion operation idempotent.
         * Webhooks may be delivered more than once.
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

        /*
         * The response is already supplied to this method as a string.
         * Normalize valid JSON when possible, but retain the original
         * response if the gateway returned non-JSON content.
         */
        var storedGatewayResponse =
            NormalizeGatewayResponse(rawGatewayResponse);

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                cancellationToken);

        try
        {
            payment.PaymentStatus = "paid";
            payment.PaymentMethod = normalizedPaymentMethod;
            payment.PaymentGateway = normalizedGateway;

            payment.TransactionReference =
                transactionReference.Trim();

            payment.GatewayPaymentId =
                string.IsNullOrWhiteSpace(gatewayPaymentId)
                    ? null
                    : gatewayPaymentId.Trim();

            payment.PaidAt = now;
            payment.GatewayFee = gatewayFee ?? 0m;
            payment.GatewayResponse = storedGatewayResponse;

            /*
             * Clear any previous failure information because
             * this payment has now completed successfully.
             */
            payment.FailureReason = null;

            financial.CustomerPaid =
                financial.TotalAmount;

            financial.ProcessingFee =
                gatewayFee ?? 0m;

            financial.FinancialStatus =
                "paid_pending_dispatch";

            financial.PayoutStatus =
                "not_ready";

            order.Status =
                OrderStatuses.WaitingForSupplier;

            order.UpdatedAt = now;

            _context.StatusHistory.AddRange(
                new StatusHistory
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    Status = OrderStatuses.PaymentPaid,
                    Notes =
                        $"{normalizedGateway} payment confirmed.",
                    CreatedAt = now
                },
                new StatusHistory
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    Status =
                        OrderStatuses.WaitingForSupplier,
                    Notes =
                        "Order moved to supplier assignment.",
                    CreatedAt = now
                });

            _context.AuditLogs.Add(
                new AuditLog
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    Action =
                        $"{normalizedGateway} Payment Confirmed",
                    PerformedBy = normalizedGateway,
                    CreatedAt = now
                });

            await _context.SaveChangesAsync(
                cancellationToken);

            await _settlements
                .CreateOrUpdateSettlementAfterPayment(
                    order.Id);

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

        var trimmedResponse = rawGatewayResponse.Trim();

        try
        {
            using var document =
                JsonDocument.Parse(trimmedResponse);

            return document.RootElement.GetRawText();
        }
        catch (JsonException)
        {
            /*
             * Some gateways may return plain text or HTML during
             * an error. Preserve that response for troubleshooting.
             */
            return trimmedResponse;
        }
    }
}