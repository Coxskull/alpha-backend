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
    private readonly ReferralCommissionService
        _referralCommissionService;

    public PaymentCompletionService(
        AppDbContext context,
        SettlementService settlements,
        ReferralCommissionService referralCommissionService)
    {
        _context = context;
        _settlements = settlements;
        _referralCommissionService =
            referralCommissionService;
    }

    public async Task CompleteOrderPaymentAsync(
        Guid orderId,
        string gateway,
        string paymentMethod,
        string transactionReference,
        string? gatewayPaymentId,
        string rawGatewayResponse,
        decimal? gatewayFee,
        CancellationToken cancellationToken)
    {
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

        if (payment.PaymentStatus == "paid")
            return;

        if (!order.CustomerId.HasValue)
        {
            throw new InvalidOperationException(
                "Order is not connected to a registered customer.");
        }

        if (payment.Amount != financial.TotalAmount)
        {
            throw new InvalidOperationException(
                "Payment and financial totals do not match.");
        }

        if (!string.Equals(
                payment.Currency,
                financial.Currency,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Payment and financial currencies do not match.");
        }

        var now = DateTime.UtcNow;

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                cancellationToken);

        try
        {
            payment.PaymentStatus = "paid";
            payment.PaymentMethod = paymentMethod;
            payment.PaymentGateway = gateway;
            payment.TransactionReference =
                transactionReference;
            payment.GatewayPaymentId =
                gatewayPaymentId;
            payment.PaidAt = now;
            payment.GatewayFee = gatewayFee ?? 0;
            payment.GatewayResponse =
                JsonDocument.Parse(rawGatewayResponse);

            financial.CustomerPaid =
                financial.TotalAmount;

            financial.ProcessingFee =
                gatewayFee ?? 0;

            financial.FinancialStatus =
                "paid_pending_dispatch";

            financial.PayoutStatus = "not_ready";

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
                        $"{gateway} payment confirmed.",
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
                        $"{gateway} Payment Confirmed",
                    PerformedBy = gateway,
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
                        $"Completed payment for order {order.OrderNumber}",
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
}