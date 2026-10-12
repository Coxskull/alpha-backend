using Alpha.API.Constants;
using Alpha.API.Data;
using Alpha.API.DTOs;
using Alpha.API.Models;
using Alpha.API.Security;
using Alpha.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
namespace Alpha.API.Controllers;

[ApiController]
[Authorize]
[Route("api/paypal")]
public class PayPalController(AppDbContext context, PayPalService paypal,
    SettlementService settlements, ReferralCommissionService referrals) : ControllerBase
{
    private PaymentOperationService Operations => new(context);

    [HttpPost("create-order")]
    public async Task<IActionResult> CreatePayPalOrder(CreatePayPalOrderDto dto, CancellationToken cancellationToken)
    {
        var order = await context.Orders.FindAsync(new object[] { dto.OrderId }, cancellationToken);
        if (order == null) return NotFound();
        if (!OrderAccess.CanPay(User, order)) return Forbid();
        var payment = await context.Payments.SingleOrDefaultAsync(p => p.OrderId == order.Id, cancellationToken);
        var financial = await context.OrderFinancials.SingleOrDefaultAsync(f => f.OrderId == order.Id, cancellationToken);
        if (payment == null || financial == null) return NotFound();
        if (payment.PaymentStatus == "paid" || order.Status != OrderStatuses.PaymentPending
            || payment.Amount != financial.TotalAmount || payment.Currency != financial.Currency)
            return Conflict(new { message = "Order is not available for checkout." });
        if (payment.PaymentMethod == "paypal" && payment.PaymentStatus == "paypal_created")
            return Ok(new { id = payment.TransactionReference, orderId = order.Id });
        try
        {
            var id = await paypal.CreateOrder(order, financial, cancellationToken);
            payment.PaymentMethod = "paypal";
            payment.PaymentGateway = "paypal";
            payment.PaymentStatus = "paypal_created";
            payment.TransactionReference = id;
            await context.SaveChangesAsync(cancellationToken);
            return Ok(new { id, orderId = order.Id, paymentStatus = payment.PaymentStatus });
        }
        catch (Exception) { return StatusCode(503, new { message = "Checkout creation could not be confirmed." }); }
    }

    [HttpPost("capture-order")]
    public async Task<IActionResult> CapturePayPalOrder(CapturePayPalOrderDto dto, CancellationToken cancellationToken)
    {
        var order = await context.Orders.FindAsync(new object[] { dto.OrderId }, cancellationToken);
        if (order == null) return NotFound();
        if (!OrderAccess.CanPay(User, order)) return Forbid();
        var payment = await context.Payments.SingleOrDefaultAsync(p => p.OrderId == order.Id, cancellationToken);
        var financial = await context.OrderFinancials.SingleOrDefaultAsync(f => f.OrderId == order.Id, cancellationToken);
        if (payment == null || financial == null) return NotFound();
        if (payment.PaymentMethod != "paypal" || payment.Amount != financial.TotalAmount || payment.Currency != financial.Currency)
            return Conflict(new { message = "PayPal payment configuration does not match the order." });
        var completed = await context.Set<PaymentOperation>().SingleOrDefaultAsync(o => o.PaymentId == payment.Id
            && o.Kind == "capture" && o.State == "completed", cancellationToken);
        if (completed != null)
            return completed.ProviderParentReference == dto.PayPalOrderId ? Ok(await ResultAsync(completed, cancellationToken)) : Conflict();
        if (order.CustomerId == null || string.IsNullOrWhiteSpace(dto.PayPalOrderId)
            || payment.TransactionReference != dto.PayPalOrderId) return BadRequest();
        return await ExecuteNewAsync(payment, "capture", "capture", payment.Amount, dto.PayPalOrderId, cancellationToken);
    }

    [HttpPost("refund")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Refund(RefundPaymentDto dto, CancellationToken cancellationToken)
    {
        // Keep the role check in the method too, so controller invocation cannot bypass it.
        if (!User.IsInRole(AppRoles.Admin)) return Forbid();
        var payment = await context.Payments.SingleOrDefaultAsync(p => p.OrderId == dto.OrderId, cancellationToken);
        if (payment == null) return NotFound();
        if (payment.PaymentMethod != "paypal" || string.IsNullOrWhiteSpace(payment.TransactionReference)) return BadRequest();
        return await ExecuteNewAsync(payment, "refund", dto.IdempotencyKey, dto.Amount,
            payment.TransactionReference, cancellationToken);
    }

    private async Task<IActionResult> ExecuteNewAsync(Payment payment, string kind, string key, decimal amount,
        string parent, CancellationToken cancellationToken)
    {
        PaymentOperation operation;
        try
        {
            var reservation = await Operations.ReserveAsync(payment.Id, kind, key, amount, payment.Currency, parent, cancellationToken);
            operation = reservation.Operation;
            if (!reservation.IsNew)
                return operation.State == "completed" ? Ok(await ResultAsync(operation, cancellationToken))
                    : Conflict(new { operationId = operation.Id, state = operation.State, message = "Operation requires reconciliation; no provider request was repeated." });
        }
        catch (ArgumentException) { return BadRequest(new { message = "A valid amount, currency and stable idempotency key are required." }); }
        catch (InvalidOperationException) { return Conflict(new { message = "Operation conflicts with payment state, balance or its original request." }); }
        string? providerId = null;
        try
        {
            using var response = kind == "capture"
                ? await paypal.CaptureOrder(parent, operation.ProviderRequestId, cancellationToken)
                : await paypal.RefundCapture(parent, amount, payment.Currency, operation.ProviderRequestId, cancellationToken);
            if (response.RootElement.ValueKind == JsonValueKind.Object
                && response.RootElement.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
                providerId = id.GetString();
            if (!await ApplyAsync(operation, response.RootElement, cancellationToken))
            {
                await Operations.RecordUncertainAsync(operation.Id, kind == "refund" ? providerId : null);
                return Accepted(new { operationId = operation.Id, state = "unknown", message = "Provider completion was not confirmed. Balance remains reserved." });
            }
            return Ok(await ResultAsync(operation, cancellationToken));
        }
        catch (Exception)
        {
            await Operations.RecordUncertainAsync(operation.Id, kind == "refund" ? providerId : null);
            return StatusCode(503, new { operationId = operation.Id, state = "unknown", message = "Provider outcome requires reconciliation. Do not submit a new operation." });
        }
    }

    [HttpPost("operations/{operationId:guid}/reconcile")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Reconcile(Guid operationId, CancellationToken cancellationToken)
    {
        if (!User.IsInRole(AppRoles.Admin)) return Forbid();
        var operation = await context.Set<PaymentOperation>().SingleOrDefaultAsync(o => o.Id == operationId, cancellationToken);
        if (operation == null) return NotFound();
        if (operation.State == "completed") return Ok(await ResultAsync(operation, cancellationToken));
        if (operation.Kind == "refund" && operation.ProviderReference == null)
            return Conflict(new { message = "Refund ID is unknown; obtain provider evidence before manual recovery. Funds remain reserved." });
        try
        {
            // Reconciliation only reads provider resources; never repeats capture or refund POSTs.
            using var response = operation.Kind == "capture"
                ? await paypal.GetOrder(operation.ProviderParentReference, cancellationToken)
                : await paypal.GetRefund(operation.ProviderReference!, cancellationToken);
            return await ApplyAsync(operation, response.RootElement, cancellationToken)
                ? Ok(await ResultAsync(operation, cancellationToken)) : Accepted(new { operationId, state = operation.State });
        }
        catch (Exception) { return StatusCode(503, new { message = "Reconciliation could not confirm provider completion." }); }
    }

    private async Task<bool> ApplyAsync(PaymentOperation operation, JsonElement response, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await Operations.LockPaymentAsync(operation.PaymentId, cancellationToken);
        await context.Entry(operation).ReloadAsync(cancellationToken);
        if (operation.State == "completed") { await transaction.CommitAsync(cancellationToken); return true; }
        var payment = await context.Payments.SingleAsync(p => p.Id == operation.PaymentId, cancellationToken);
        await context.Entry(payment).ReloadAsync(cancellationToken);
        var order = await context.Orders.SingleAsync(o => o.Id == payment.OrderId, cancellationToken);
        var financial = await context.OrderFinancials.SingleAsync(f => f.OrderId == order.Id, cancellationToken);
        await context.Entry(order).ReloadAsync(cancellationToken);
        await context.Entry(financial).ReloadAsync(cancellationToken);
        string? providerId;
        if (operation.Kind == "capture")
        {
            if (payment.PaymentStatus == "paid" || order.CustomerId == null || payment.Amount != operation.Amount
                || financial.TotalAmount != operation.Amount || financial.Currency != operation.Currency
                || !PayPalCaptureValidator.TryValidate(response, operation.ProviderParentReference, order.Id,
                    operation.Amount, operation.Currency, out providerId)) return false;
            payment.PaymentStatus = "paid";
            payment.TransactionReference = providerId;
            payment.GatewayPaymentId = providerId;
            payment.PaidAt = DateTime.UtcNow;
            financial.CustomerPaid = operation.Amount;
            financial.FinancialStatus = "paid_pending_dispatch";
            financial.PayoutStatus = "not_ready";
            order.Status = OrderStatuses.WaitingForSupplier;
            context.StatusHistory.Add(new StatusHistory { Id = Guid.NewGuid(), OrderId = order.Id,
                Status = OrderStatuses.PaymentPaid, CreatedAt = DateTime.UtcNow });
            context.StatusHistory.Add(new StatusHistory { Id = Guid.NewGuid(), OrderId = order.Id,
                Status = OrderStatuses.WaitingForSupplier, CreatedAt = DateTime.UtcNow });
            await context.SaveChangesAsync(cancellationToken);
            await settlements.CreateOrUpdateSettlementAfterPayment(order.Id, cancellationToken);
            await referrals.GenerateOrderCommissionAsync(order.CustomerId.Value, order.Id, payment.Id,
                payment.Amount, payment.Currency, "customer_order", $"Completed payment for order {order.OrderNumber}", cancellationToken);
        }
        else
        {
            if (!PayPalRefundValidator.TryValidate(response, operation.Amount, operation.Currency,
                operation.ProviderParentReference, out var refundId)
                || (operation.ProviderReference != null && operation.ProviderReference != refundId)
                || payment.RefundedAmount + operation.Amount > payment.Amount) return false;
            providerId = refundId;
            payment.RefundedAmount += operation.Amount;
            payment.RefundReference = refundId;
            payment.RefundedAt = DateTime.UtcNow;
            var full = payment.RefundedAmount == payment.Amount;
            payment.RefundStatus = full ? "fully_refunded" : "partially_refunded";
            if (full) { payment.PaymentStatus = "refunded"; order.Status = "refunded"; }
            financial.RefundAmount = payment.RefundedAmount;
            // Partial refund allocation needs financial review; never release unadjusted payouts.
            financial.FinancialStatus = "refund_review";
            financial.PayoutStatus = "on_hold";
            financial.SettlementStatus = "blocked";
            var queues = await context.SettlementQueue.Where(q => q.OrderFinancialId == financial.Id && q.Status != "paid").ToListAsync(cancellationToken);
            foreach (var queue in queues) queue.Status = "on_hold";
            context.StatusHistory.Add(new StatusHistory { Id = Guid.NewGuid(), OrderId = order.Id,
                Status = payment.RefundStatus, CreatedAt = DateTime.UtcNow });
            if (full) await referrals.ReverseOrderCommissionsAsync(order.Id, "Customer payment fully refunded.", cancellationToken);
        }
        operation.State = "completed";
        operation.ProviderReference = providerId;
        operation.UpdatedAt = DateTime.UtcNow;
        order.UpdatedAt = DateTime.UtcNow;
        context.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), OrderId = order.Id,
            Action = "PayPal " + operation.Kind + " completed", PerformedBy = "paypal", CreatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private async Task<object> ResultAsync(PaymentOperation operation, CancellationToken cancellationToken)
    {
        var payment = await context.Payments.AsNoTracking().SingleAsync(p => p.Id == operation.PaymentId, cancellationToken);
        var order = await context.Orders.AsNoTracking().SingleAsync(o => o.Id == payment.OrderId, cancellationToken);
        return new { operationId = operation.Id, state = operation.State, providerId = operation.ProviderReference,
            amount = operation.Amount, currency = operation.Currency, orderId = order.Id, orderNumber = order.OrderNumber,
            orderStatus = order.Status, paymentStatus = payment.PaymentStatus,
            captureId = operation.Kind == "capture" ? operation.ProviderReference : null,
            refundId = operation.Kind == "refund" ? operation.ProviderReference : null,
            transactionReference = payment.TransactionReference, refundedAmount = payment.RefundedAmount,
            remainingRefundableAmount = payment.Amount - payment.RefundedAmount, refundStatus = payment.RefundStatus };
    }

    [HttpGet("order/{orderId:guid}")]
    public async Task<IActionResult> GetByOrder(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await context.Orders.FindAsync(new object[] { orderId }, cancellationToken);
        if (order == null) return NotFound();
        if (!OrderAccess.CanPay(User, order)) return Forbid();
        var payment = await context.Payments.SingleOrDefaultAsync(p => p.OrderId == orderId, cancellationToken);
        return payment == null ? NotFound() : Ok(new { payment.Id, payment.OrderId, payment.Amount,
            payment.Currency, payment.PaymentStatus, payment.RefundedAmount, payment.RefundStatus });
    }

    [HttpPost("orders/{orderId:guid}/verify-settlement")]
    [Authorize(Roles = "admin,dispatcher")]
    public async Task<IActionResult> VerifySettlement(Guid orderId, CancellationToken cancellationToken)
        => Ok(await settlements.VerifySettlement(orderId));
}
