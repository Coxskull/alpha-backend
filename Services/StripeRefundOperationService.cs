using Alpha.API.Data;
using Alpha.API.Models;
using Microsoft.EntityFrameworkCore;
using Stripe;
namespace Alpha.API.Services;

public enum StripeRefundEvidenceState { Incomplete, Verified, Conflict }

public sealed class StripeRefundOperationService(AppDbContext db)
{
    // Caller owns the payment lock and transaction; signed webhook verification precedes this call.
    public async Task<StripeRefundEvidenceState> RegisterAsync(Payment payment, Charge charge, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Refund evidence requires a payment transaction.");
        if (charge.Refunds == null || charge.Refunds.HasMore || charge.Refunds.Data == null) return StripeRefundEvidenceState.Incomplete;
        if (!StripeRefundEvidenceValidator.TryGetCompleted(charge, payment.Currency, out var refunds)) return StripeRefundEvidenceState.Conflict;
        var prior = await db.Set<PaymentOperation>().AsNoTracking().Where(o => o.PaymentId == payment.Id && o.Kind == "refund" && o.State == "completed").ToListAsync(ct);
        var ids = refunds.Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        if (charge.AmountRefunded / 100m >= payment.RefundedAmount
            && prior.Any(o => o.ProviderParentReference != charge.Id || o.ProviderReference == null || !ids.Contains(o.ProviderReference)))
            return StripeRefundEvidenceState.Conflict;
        var newAmount = refunds.Where(r => !prior.Any(o => o.ProviderReference == r.Id)).Sum(r => r.Amount / 100m);
        if (prior.Sum(o => o.Amount) + newAmount > Math.Max(payment.RefundedAmount, charge.AmountRefunded / 100m))
            return StripeRefundEvidenceState.Conflict;
        var additions = new List<PaymentOperation>();
        foreach (var refund in refunds)
        {
            var amount = refund.Amount / 100m;
            var key = "stripe:refund:" + refund.Id;
            var existing = await db.Set<PaymentOperation>().SingleOrDefaultAsync(o => o.Kind == "refund" && o.ProviderReference == refund.Id, ct);
            if (existing != null)
            {
                await db.Entry(existing).ReloadAsync(ct);
                if (existing.PaymentId != payment.Id || existing.State != "completed" || existing.Amount != amount
                    || existing.Currency != payment.Currency || existing.ProviderParentReference != charge.Id || existing.IdempotencyKey != key)
                    return StripeRefundEvidenceState.Conflict;
                continue;
            }
            if (await db.Set<PaymentOperation>().AnyAsync(o => o.PaymentId == payment.Id && o.Kind == "refund" && o.IdempotencyKey == key, ct))
                return StripeRefundEvidenceState.Conflict;
            additions.Add(new PaymentOperation { Id = Guid.NewGuid(), PaymentId = payment.Id,
                Kind = "refund", IdempotencyKey = key, Amount = amount, Currency = payment.Currency, State = "completed",
                // Internal identity only: no provider request/POST is issued for webhook evidence.
                ProviderRequestId = Guid.NewGuid().ToString("N"), ProviderReference = refund.Id,
                ProviderParentReference = charge.Id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        }
        db.AddRange(additions);
        return StripeRefundEvidenceState.Verified;
    }
}
