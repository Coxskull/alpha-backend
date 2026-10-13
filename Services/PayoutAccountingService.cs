using Alpha.API.Data;
using Alpha.API.DTOs;
using Alpha.API.Models;
using Alpha.API.Services.Entrepreneur;
using Microsoft.EntityFrameworkCore;
namespace Alpha.API.Services;

public sealed class PayoutAccountingService(AppDbContext db)
{
    public static Task<bool> HasPendingStripeRefundAsync(AppDbContext db, Guid orderId, CancellationToken ct)
        => db.Database.SqlQuery<bool>($$"""
            SELECT EXISTS (
                SELECT 1 FROM payment_webhook_events e JOIN payments p ON
                    (e.payload #>> '{data,object,payment_intent}' = p.gateway_payment_id
                    OR e.payload #>> '{data,object,payment_intent,id}' = p.gateway_payment_id)
                WHERE p.order_id = {{orderId}} AND LOWER(p.payment_gateway) = 'stripe'
                    AND e.gateway = 'stripe' AND e.event_type = 'charge.refunded' AND e.processed = FALSE
            ) AS "Value"
            """).SingleAsync(ct);

    public static async Task LockOrderAsync(AppDbContext db, Guid orderId, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM payments WHERE order_id = {orderId} ORDER BY id FOR UPDATE", ct);
        if (await HasPendingStripeRefundAsync(db, orderId, ct)) throw new InvalidOperationException("Payout is held for an unprocessed signed Stripe refund event.");
        if (await RefundConflictGuard.HasAsync(db, orderId, ct)) throw new InvalidOperationException("Payout is held for conflicting refund evidence.");
        var financial = await db.OrderFinancials.AsNoTracking().SingleAsync(f => f.OrderId == orderId, ct);
        if (financial.RefundAmount != 0 || financial.ChargebackAmount != 0 || financial.DisputeReserve != 0
            || financial.FinancialStatus != "verified" || financial.PayoutStatus is not ("ready_for_payout" or "paid")
            || await db.Payments.AnyAsync(p => p.OrderId == orderId && p.RefundedAmount > 0, ct)
            || !await db.Payments.AnyAsync(p => p.OrderId == orderId && p.PaymentStatus == "paid" && p.RefundedAmount == 0, ct)
            || await db.Set<PaymentOperation>().AnyAsync(o => o.Kind == "refund" && o.State != "failed"
                && db.Payments.Any(p => p.Id == o.PaymentId && p.OrderId == orderId), ct))
            throw new InvalidOperationException("Payout is held for unresolved payment, refund, dispute or financial review.");
    }

    public async Task<object> SettlementAsync(Guid id, PayoutConfirmationDto? confirmation, Guid admin,
        EntrepreneurCommissionService? commissions, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var initial = await db.SettlementQueue.AsNoTracking().SingleAsync(q => q.Id == id, ct);
            var financial = await db.OrderFinancials.SingleAsync(f => f.Id == initial.OrderFinancialId, ct);
            if (financial.OrderId == null || initial.PayeeId == null || initial.PayeeType is not ("supplier" or "driver"))
                throw new InvalidOperationException("Unsupported or unattributed payout requires manual review.");
            var recovery = new RefundAllocationService(db);
            await LockOrderAsync(db, financial.OrderId.Value, ct);
            await db.Entry(financial).ReloadAsync(ct);
            await recovery.LockPayeeAsync(initial.PayeeType, initial.PayeeId.Value, financial.Currency, ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM settlement_queue WHERE id = {id} FOR UPDATE", ct);
            var queue = await db.SettlementQueue.SingleAsync(q => q.Id == id, ct); await db.Entry(queue).ReloadAsync(ct);
            if (queue.Status == "paid")
            {
                var paidResult = new { settlementId = id, grossAmount = queue.Amount, refundAdjustment = queue.RefundAdjustment,
                    recoveryOffset = queue.RecoveryOffset, paidAmount = queue.Amount - queue.RefundAdjustment - queue.RecoveryOffset, financial.Currency, status = "paid" };
                if (confirmation != null && (confirmation.PaidAmount != paidResult.paidAmount || confirmation.Currency != financial.Currency))
                    throw PayoutOperatorContract.Error("payout_replay_conflict", "Replay does not match the recorded payout.");
                if (confirmation != null && !await db.AuditLogs.AnyAsync(a => a.OrderId == financial.OrderId
                    && a.Action.StartsWith("Payout recorded " + id + ";") && a.Action.EndsWith("; reference=" + confirmation.PayoutReference), ct))
                    throw PayoutOperatorContract.Error("payout_replay_conflict", "Replay reference does not match the recorded payout.");
                await tx.CommitAsync(ct); return paidResult;
            }
            if (queue.Status is not ("ready_for_payout" or "pending_payout") || !RefundAllocationService.Money(queue.Amount - queue.RefundAdjustment))
                throw new InvalidOperationException("Settlement is not ready for payout.");
            var payable = queue.Amount - queue.RefundAdjustment;
            var outstanding = await PayoutOperatorContract.OutstandingAsync(db, queue.PayeeType, queue.PayeeId!.Value, financial.Currency, ct);
            var offset = await recovery.RecoverAsync(queue.PayeeType, queue.PayeeId!.Value, financial.Currency, "settlement", id, payable, ct);
            var net = payable - offset;
            var result = new { settlementId = id, grossAmount = queue.Amount, refundAdjustment = queue.RefundAdjustment,
                recoveryOffset = offset, payableBeforeRecovery = payable, paidAmount = net, netAmount = net, remainingRecoveryBalance = outstanding - offset,
                zeroNet = net == 0, quoteIsAdvisory = confirmation == null, confirmationRequired = confirmation == null,
                eligibleForConfirmation = confirmation == null, financial.Currency, status = confirmation == null ? "quote" : "paid" };
            if (confirmation == null) { await tx.RollbackAsync(ct); return result; }
            if (admin == Guid.Empty || confirmation.PaidAmount == null || string.IsNullOrWhiteSpace(confirmation.Currency)
                || string.IsNullOrWhiteSpace(confirmation.PayoutReference) || confirmation.PayoutReference.Length > 128)
                throw PayoutOperatorContract.Error("invalid_confirmation", "Supply the net amount, currency and payout reference.");
            if (confirmation.PaidAmount != net || confirmation.Currency != financial.Currency)
                throw PayoutOperatorContract.Error("payout_quote_changed", "Refresh the quote and confirm its current net amount and currency; gross payout is not permitted.");
            var duplicate = queue.PayeeType == "supplier"
                ? await db.SupplierPayouts.AnyAsync(p => p.OrderId == financial.OrderId && p.SupplierId == queue.PayeeId, ct)
                : await db.DriverPayouts.AnyAsync(p => p.OrderId == financial.OrderId && p.DriverId == queue.PayeeId, ct);
            if (duplicate) throw new InvalidOperationException("An existing payout requires manual reconciliation.");
            if (queue.PayeeType == "supplier")
                db.SupplierPayouts.Add(new SupplierPayout { Id = Guid.NewGuid(), SupplierId = queue.PayeeId.Value, OrderId = financial.OrderId.Value,
                    Amount = net, Currency = financial.Currency, PayoutStatus = "paid", PaidAt = DateTime.UtcNow });
            else
                db.DriverPayouts.Add(new DriverPayout { Id = Guid.NewGuid(), DriverId = queue.PayeeId.Value, OrderId = financial.OrderId.Value,
                    Amount = net, Currency = financial.Currency, PayoutStatus = "paid", PaidAt = DateTime.UtcNow });
            queue.RecoveryOffset = offset; queue.Status = "paid"; queue.ReviewedBy = admin.ToString(); queue.ReviewedAt = DateTime.UtcNow;
            await db.Entry(financial).ReloadAsync(ct);
            var remaining = await db.SettlementQueue.AnyAsync(q => q.OrderFinancialId == financial.Id && q.Id != id && q.Status != "paid", ct);
            if (!remaining) { financial.PayoutStatus = "paid"; financial.SettlementStatus = "paid"; financial.ProviderPayoutStatus = "paid"; }
            db.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), OrderId = financial.OrderId.Value,
                Action = $"Payout recorded {id}; net={net}; recovery={offset}; reference={confirmation.PayoutReference}", PerformedBy = admin.ToString(), CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync(ct);
            if (commissions != null)
            {
                var order = await db.Orders.SingleAsync(o => o.Id == financial.OrderId, ct);
                await commissions.GenerateForPaidSettlementAsync(queue, order, financial, ct);
                if (!remaining) await commissions.GenerateForOrderAsync(order.Id, ct);
            }
            await tx.CommitAsync(ct); return result;
        }
        finally { db.ChangeTracker.Clear(); }
    }
}
