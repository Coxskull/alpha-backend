using Alpha.API.Data;
using Alpha.API.DTOs;
using Alpha.API.Models;
using Microsoft.EntityFrameworkCore;
namespace Alpha.API.Services;

// A release authorizes one existing claim only, never the original order or a new allocation.
public sealed class FinancialRecoveryReleaseService(AppDbContext db)
{
    public async Task<object> ReleaseAsync(Guid reviewId, FinancialRecoveryReleaseDto request, Guid actor, CancellationToken ct)
    {
        if (request.AllocationId == Guid.Empty || string.IsNullOrWhiteSpace(request.Evidence) || request.Evidence.Trim().Length > 4000)
            throw new ArgumentException("An existing allocation and nonblank release evidence of at most 4000 characters are required.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var reviews = new FinancialConflictReviewService(db);
        await reviews.AdministratorAsync(actor, ct);
        var review = await db.Set<FinancialConflictReview>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == reviewId, ct)
            ?? throw new InvalidOperationException("Review not found.");
        var orderId = await reviews.LockOrderAsync(review.PaymentId, ct);
        var allocation = await db.Set<RefundAllocation>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.AllocationId, ct)
            ?? throw new InvalidOperationException("Recovery claim not found.");
        var operation = await db.Set<PaymentOperation>().AsNoTracking().SingleAsync(x => x.Id == allocation.OperationId, ct);
        if (operation.PaymentId != review.PaymentId || operation.Kind != "refund" || operation.State != "completed"
            || !allocation.FutureRecovery || allocation.Amount <= 0)
            throw new InvalidOperationException("Only an existing future-recovery claim on the reviewed payment is eligible.");
        await new RefundAllocationService(db).LockPayeeAsync(allocation.PayeeType, allocation.PayeeId, allocation.Currency, ct);
        var existing = await db.Set<FinancialRecoveryRelease>().AsNoTracking().SingleOrDefaultAsync(x => x.ReviewId == reviewId && x.AllocationId == allocation.Id, ct);
        if (existing is not null)
        {
            if (existing.ReviewerId != actor || existing.Evidence != request.Evidence.Trim())
                throw new InvalidOperationException("Recorded release cannot be changed or reassigned.");
            var effective = allocation.RecoveredAmount < allocation.Amount && !await PayoutAccountingService.HasPendingStripeRefundAsync(db, orderId, ct)
                && existing.SourceFingerprint == await reviews.SnapshotAsync(orderId, ct, recovery: true);
            await tx.CommitAsync(ct); return new { release = existing, scope = "existing_future_recovery_claim_only", effective, originalOrderHoldReleased = false };
        }
        if (await PayoutAccountingService.HasPendingStripeRefundAsync(db, orderId, ct))
            throw new InvalidOperationException("A pending signed refund event must finish processing before claim release.");
        var approval = await db.Set<FinancialConflictApproval>().AsNoTracking().SingleOrDefaultAsync(x => x.ReviewId == reviewId, ct)
            ?? throw new InvalidOperationException("Two administrator attestations are required.");
        if (actor != review.FirstReviewerId && actor != approval.ReviewerId || review.FirstReviewerId == approval.ReviewerId)
            throw new InvalidOperationException("A distinct attesting administrator must record this claim release.");
        await reviews.AdministratorAsync(review.FirstReviewerId, ct);
        await reviews.AdministratorAsync(approval.ReviewerId, ct);
        // Recovery can progress under this payee lock; always reload after acquiring it.
        allocation = await db.Set<RefundAllocation>().AsNoTracking().SingleAsync(x => x.Id == request.AllocationId, ct);
        if (allocation.RecoveredAmount >= allocation.Amount || review.OrderId != orderId
            || review.SnapshotHash != await reviews.SnapshotAsync(orderId, ct))
            throw new InvalidOperationException("The reviewed financial evidence changed or the claim is fully recovered. A fresh review is required.");
        if (!await db.OrderFinancials.AnyAsync(x => x.OrderId == orderId, ct)) throw new InvalidOperationException("Source financial evidence is missing.");
        await ValidatePaidSourceAsync(allocation, orderId, ct);
        var release = new FinancialRecoveryRelease { Id = Guid.NewGuid(), ReviewId = reviewId, AllocationId = allocation.Id,
            ReviewerId = actor, Evidence = request.Evidence.Trim(), SourceFingerprint = await reviews.SnapshotAsync(orderId, ct, recovery: true), CreatedAt = DateTime.UtcNow };
        db.Add(release);
        db.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), OrderId = orderId, Action = "Existing recovery claim release recorded: " + release.Id,
            PerformedBy = actor.ToString(), CreatedAt = release.CreatedAt });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new { release, scope = "existing_future_recovery_claim_only", effective = true, originalOrderHoldReleased = false };
    }

    private async Task ValidatePaidSourceAsync(RefundAllocation claim, Guid orderId, CancellationToken ct)
    {
        decimal capacity;
        if (claim.SourceKind == "settlement")
        {
            var source = await db.SettlementQueue.AsNoTracking().SingleOrDefaultAsync(x => x.Id == claim.SourceId, ct);
            var financialId = await db.OrderFinancials.Where(x => x.OrderId == orderId).Select(x => x.Id).SingleAsync(ct);
            if (source is null || source.OrderFinancialId != financialId || source.Status != "paid"
                || source.PayeeId != claim.PayeeId || source.PayeeType != claim.PayeeType)
                throw new InvalidOperationException("Original paid source evidence is missing or mismatched.");
            var paid = claim.PayeeType == "supplier"
                ? await db.SupplierPayouts.Where(x => x.OrderId == orderId && x.SupplierId == claim.PayeeId && x.Currency == claim.Currency && x.PayoutStatus == "paid").Select(x => x.Amount).ToListAsync(ct)
                : claim.PayeeType == "driver"
                    ? await db.DriverPayouts.Where(x => x.OrderId == orderId && x.DriverId == claim.PayeeId && x.Currency == claim.Currency && x.PayoutStatus == "paid").Select(x => x.Amount).ToListAsync(ct)
                    : new List<decimal>();
            if (paid.Count != 1) throw new InvalidOperationException("Original paid source requires unambiguous payout history.");
            capacity = paid[0];
        }
        else if (claim.SourceKind == "earning")
        {
            var source = await db.EntrepreneurEarnings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == claim.SourceId, ct);
            var batch = source is null ? null : await db.EntrepreneurPayoutBatches.AsNoTracking().SingleOrDefaultAsync(x => x.Id == source.PayoutBatchId, ct);
            if (source is null || source.OrderId != orderId || source.Currency != claim.Currency || source.EntrepreneurUserId != claim.PayeeId
                || claim.PayeeType != "entrepreneur" || batch is null || batch.Status != "PAID" || batch.EarningCount != 1 || batch.Currency != claim.Currency
                || source.PayoutDate == null && !source.EarningStatus.Equals("PAID", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Original paid earning evidence is missing or mismatched.");
            capacity = batch.TotalAmount;
        }
        else throw new InvalidOperationException("Unsupported recovery source.");
        var attributed = await db.Set<RefundAllocation>().Where(x => x.SourceKind == claim.SourceKind && x.SourceId == claim.SourceId && x.FutureRecovery).SumAsync(x => x.Amount, ct);
        if (capacity <= 0 || capacity != decimal.Round(capacity, 2) || attributed > capacity)
            throw new InvalidOperationException("Recovery attribution exceeds the verified original paid amount.");
    }

    // Caller owns the payout transaction and payee lock. Provider conflicts use the same lock before commit.
    internal static async Task<bool> EffectiveAsync(AppDbContext db, Guid allocationId, Guid orderId, CancellationToken ct)
    {
        if (!await db.Set<FinancialRecoveryRelease>().AnyAsync(x => x.AllocationId == allocationId, ct)) return false;
        var fingerprint = await new FinancialConflictReviewService(db).SnapshotAsync(orderId, ct, recovery: true);
        return await db.Set<FinancialRecoveryRelease>().AnyAsync(x => x.AllocationId == allocationId && x.SourceFingerprint == fingerprint, ct);
    }
}
