using Alpha.API.Data;
using Alpha.API.Models;
using Microsoft.EntityFrameworkCore;
namespace Alpha.API.Services;

public sealed class PaymentOperationService(AppDbContext db)
{
    public async Task<(PaymentOperation Operation, bool IsNew)> ReserveAsync(Guid paymentId, string kind, string key,
        decimal amount, string currency, string parentReference, CancellationToken cancellationToken)
    {
        if (kind is not ("capture" or "refund") || string.IsNullOrWhiteSpace(key) || key.Length > 128
            || amount <= 0 || decimal.Round(amount, 2) != amount || currency.Length != 3)
            throw new ArgumentException("Invalid payment operation request.");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockPaymentAsync(paymentId, cancellationToken);
        var payment = await db.Payments.SingleAsync(p => p.Id == paymentId, cancellationToken);
        await db.Entry(payment).ReloadAsync(cancellationToken);
        if (payment.OrderId == null) throw new InvalidOperationException("Unassociated payments require manual review.");
        var existing = await db.Set<PaymentOperation>().SingleOrDefaultAsync(o =>
            o.PaymentId == paymentId && o.Kind == kind && o.IdempotencyKey == key, cancellationToken);
        if (existing != null)
        {
            if (existing.Amount != amount || existing.Currency != currency || existing.ProviderParentReference != parentReference)
                throw new InvalidOperationException("Idempotency key conflicts with the original request.");
            await transaction.CommitAsync(cancellationToken);
            return (existing, false);
        }
        var outstanding = await db.Set<PaymentOperation>().Where(o => o.PaymentId == paymentId
            && o.Kind == kind && o.State != "completed" && o.State != "failed").SumAsync(o => o.Amount, cancellationToken);
        if (kind == "capture" && (payment.PaymentStatus == "paid" || outstanding > 0))
            throw new InvalidOperationException("Capture is completed or requires reconciliation.");
        if (kind == "refund" && (payment.PaymentStatus != "paid" || amount > payment.Amount - payment.RefundedAmount - outstanding))
            throw new InvalidOperationException("Refund exceeds the available balance or payment is not paid.");

        if (kind == "refund")
        {
            var financial = await db.OrderFinancials.SingleAsync(f => f.OrderId == payment.OrderId, cancellationToken);
            financial.PayoutStatus = "on_hold";
            financial.SettlementStatus = "blocked";
            financial.FinancialStatus = "refund_review";
            var queues = await db.SettlementQueue.Where(q => q.OrderFinancialId == financial.Id && q.Status != "paid").ToListAsync(cancellationToken);
            foreach (var queue in queues) queue.Status = "on_hold";
        }
        var operation = new PaymentOperation
        {
            Id = Guid.NewGuid(), PaymentId = paymentId, Kind = kind, IdempotencyKey = key,
            Amount = amount, Currency = currency, ProviderParentReference = parentReference,
            ProviderRequestId = Guid.NewGuid().ToString("N"), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.Add(operation);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (operation, true);
    }

    public Task<int> LockPaymentAsync(Guid paymentId, CancellationToken cancellationToken)
        => db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM payments WHERE id = {paymentId} FOR UPDATE", cancellationToken);

    public async Task RecordUncertainAsync(Guid operationId, string? providerReference = null)
    {
        // Clear rolled-back business changes before independently retaining the uncertain outcome.
        db.ChangeTracker.Clear();
        await db.Set<PaymentOperation>().Where(o => o.Id == operationId && o.State != "completed")
            .ExecuteUpdateAsync(setters => setters.SetProperty(o => o.State, "unknown")
                .SetProperty(o => o.ProviderReference, providerReference)
                .SetProperty(o => o.UpdatedAt, DateTime.UtcNow));
    }
}
