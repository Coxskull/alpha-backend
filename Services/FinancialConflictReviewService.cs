using Alpha.API.Data;
using Alpha.API.DTOs;
using Alpha.API.Models;
using Alpha.API.Security;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace Alpha.API.Services;

// Evidence attestations only. This service never clears a flag or releases money.
public sealed class FinancialConflictReviewService(AppDbContext db)
{
    private static string Evidence(string value)
        => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 4000
            ? value.Trim() : throw new ArgumentException("Evidence is required and must not exceed 4000 characters.");

    internal async Task AdministratorAsync(Guid actor, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actor, ct);
        var roles = await db.UserRoles.AsNoTracking().Where(x => x.UserId == actor).ToListAsync(ct);
        if (user is null || !user.IsActive || !ActiveRoles.Select(user.Role, roles).Contains("admin"))
            throw new UnauthorizedAccessException("An active administrator is required.");
    }

    internal async Task<Guid> LockOrderAsync(Guid paymentId, CancellationToken ct)
    {
        var orderId = await db.Payments.AsNoTracking().Where(x => x.Id == paymentId).Select(x => x.OrderId).SingleOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("A payment with an attributable order is required.");
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM payments WHERE order_id = {orderId} ORDER BY id FOR UPDATE", ct);
        var payment = await db.Payments.AsNoTracking().SingleAsync(x => x.Id == paymentId, ct);
        if (payment.OrderId != orderId || !payment.RefundEvidenceConflict)
            throw new InvalidOperationException("Only a durably conflicted payment can be reviewed.");
        return orderId;
    }

    internal async Task<string> SnapshotAsync(Guid orderId, CancellationToken ct, bool recovery = false)
    {
        var order = await db.Orders.AsNoTracking().Where(x => x.Id == orderId)
            .Select(x => new { x.Id, x.CustomerId, x.Currency, x.CountryCode, x.Status }).SingleAsync(ct);
        var payments = await db.Payments.AsNoTracking().Where(x => x.OrderId == orderId).OrderBy(x => x.Id).ToListAsync(ct);
        var paymentIds = payments.Select(x => x.Id).ToArray();
        var operations = await db.Set<PaymentOperation>().AsNoTracking().Where(x => paymentIds.Contains(x.PaymentId)).OrderBy(x => x.Id).ToListAsync(ct);
        var operationIds = operations.Select(x => x.Id).ToArray();
        var allocations = await db.Set<RefundAllocation>().AsNoTracking().Where(x => operationIds.Contains(x.OperationId)).OrderBy(x => x.Id).ToListAsync(ct);
        var allocationIds = allocations.Select(x => x.Id).ToArray();
        var offsets = await db.Set<PayoutRecoveryOffset>().AsNoTracking().Where(x => allocationIds.Contains(x.AllocationId)).OrderBy(x => x.Id).ToListAsync(ct);
        var financials = await db.OrderFinancials.AsNoTracking().Where(x => x.OrderId == orderId).OrderBy(x => x.Id).ToListAsync(ct);
        var conflicts = await db.AuditLogs.AsNoTracking().Where(x => x.OrderId == orderId && x.Action == "Stripe refund evidence conflict held for manual review").OrderBy(x => x.Id).ToListAsync(ct);
        var financialIds = financials.Select(x => x.Id).ToArray();
        var queues = await db.SettlementQueue.AsNoTracking().Where(x => financialIds.Contains(x.OrderFinancialId)).OrderBy(x => x.Id).ToListAsync(ct);
        var supplierPaid = await db.SupplierPayouts.AsNoTracking().Where(x => x.OrderId == orderId).OrderBy(x => x.Id).ToListAsync(ct);
        var driverPaid = await db.DriverPayouts.AsNoTracking().Where(x => x.OrderId == orderId).OrderBy(x => x.Id).ToListAsync(ct);
        var earnings = await db.EntrepreneurEarnings.AsNoTracking().Where(x => x.OrderId == orderId).OrderBy(x => x.Id).ToListAsync(ct);
        var batchIds = earnings.Where(x => x.PayoutBatchId != null).Select(x => x.PayoutBatchId).ToArray();
        var batches = await db.EntrepreneurPayoutBatches.AsNoTracking().Where(x => batchIds.Contains(x.Id)).OrderBy(x => x.Id).ToListAsync(ct);
        // Ordinary recovery progress must not revoke a claim-specific authorization.
        // All immutable claim identities/amounts/evidence and source state remain bound.
        object allocationEvidence = recovery ? allocations.Select(x => new { x.Id, x.OperationId, x.SourceKind, x.SourceId,
            x.PayeeType, x.PayeeId, x.Currency, x.Amount, x.FutureRecovery, x.ReviewedBy, x.Evidence, x.CreatedAt }).ToArray() : allocations;
        object offsetEvidence = recovery ? Array.Empty<PayoutRecoveryOffset>() : offsets;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { order, payments, operations,
            snapshotVersion = 2, allocations = allocationEvidence, offsets = offsetEvidence, financials, conflicts, queues, supplierPaid, driverPaid, earnings, batches }))));
    }

    public async Task<FinancialConflictReview> SubmitAsync(Guid paymentId, FinancialConflictReviewDto request, Guid actor, CancellationToken ct)
    {
        var evidence = Evidence(request.Evidence);
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Trim().Length > 128)
            throw new ArgumentException("A stable idempotency key of at most 128 characters is required.");
        var key = request.IdempotencyKey.Trim();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await AdministratorAsync(actor, ct);
        var orderId = await LockOrderAsync(paymentId, ct);
        var existing = await db.Set<FinancialConflictReview>().AsNoTracking().SingleOrDefaultAsync(x => x.PaymentId == paymentId && x.IdempotencyKey == key, ct);
        if (existing is not null)
        {
            if (existing.FirstReviewerId != actor || existing.Evidence != evidence)
                throw new InvalidOperationException("An existing review cannot be changed or reassigned.");
            await tx.CommitAsync(ct); return existing;
        }
        var review = new FinancialConflictReview { Id = Guid.NewGuid(), PaymentId = paymentId, OrderId = orderId,
            FirstReviewerId = actor, IdempotencyKey = key, Evidence = evidence, SnapshotHash = await SnapshotAsync(orderId, ct), CreatedAt = DateTime.UtcNow };
        db.Add(review);
        db.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), OrderId = orderId, Action = "Financial conflict first attestation " + review.Id,
            PerformedBy = actor.ToString(), CreatedAt = review.CreatedAt });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return review;
    }

    public async Task<FinancialConflictApproval> ApproveAsync(Guid reviewId, FinancialConflictApprovalDto request, Guid actor, CancellationToken ct)
    {
        var evidence = Evidence(request.Evidence);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await AdministratorAsync(actor, ct);
        var review = await db.Set<FinancialConflictReview>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == reviewId, ct)
            ?? throw new InvalidOperationException("Review not found.");
        var orderId = await LockOrderAsync(review.PaymentId, ct);
        if (review.FirstReviewerId == actor) throw new InvalidOperationException("A different administrator must supply the second attestation.");
        await AdministratorAsync(review.FirstReviewerId, ct);
        if (orderId != review.OrderId || review.SnapshotHash != await SnapshotAsync(orderId, ct))
            throw new InvalidOperationException("Financial evidence changed. Submit a new review with a new key.");
        var existing = await db.Set<FinancialConflictApproval>().AsNoTracking().SingleOrDefaultAsync(x => x.ReviewId == reviewId, ct);
        if (existing is not null)
        {
            if (existing.ReviewerId != actor || existing.Evidence != evidence) throw new InvalidOperationException("The recorded second attestation cannot be changed or reassigned.");
            await tx.CommitAsync(ct); return existing;
        }
        var approval = new FinancialConflictApproval { Id = Guid.NewGuid(), ReviewId = reviewId, ReviewerId = actor, Evidence = evidence, CreatedAt = DateTime.UtcNow };
        db.Add(approval);
        db.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), OrderId = orderId, Action = "Financial conflict second attestation " + review.Id,
            PerformedBy = actor.ToString(), CreatedAt = approval.CreatedAt });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return approval;
    }

    public async Task<object> StatusAsync(Guid reviewId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var review = await db.Set<FinancialConflictReview>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == reviewId, ct)
            ?? throw new InvalidOperationException("Review not found.");
        var orderId = await LockOrderAsync(review.PaymentId, ct);
        var approval = await db.Set<FinancialConflictApproval>().AsNoTracking().SingleOrDefaultAsync(x => x.ReviewId == reviewId, ct);
        var unchanged = orderId == review.OrderId && review.SnapshotHash == await SnapshotAsync(orderId, ct);
        await tx.CommitAsync(ct);
        return new { review, approval, evidenceUnchanged = unchanged,
            status = !unchanged ? "evidence_changed" : approval is null ? "awaiting_second_administrator" : "two_attestations_recorded",
            holdReleased = false };
    }
}
