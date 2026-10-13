using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Alpha.API.Data;
using Alpha.API.DTOs;
using Alpha.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Alpha.API.Services;

// Accounting only. This service has no bank/provider client and never moves money.
public sealed class RefundAllocationService(AppDbContext db)
{
    public static bool Money(decimal value) => value >= 0 && value <= 9999999999999999.99m && decimal.Round(value, 2) == value;

    public async Task LockPayeeAsync(string type, Guid id, string currency, CancellationToken ct)
    {
        var identity = $"refund-recovery:{type}:{id:D}:{currency}";
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({identity}, 0))", ct);
    }

    public async Task AllocateAsync(Guid operationId, RefundAllocationDto plan, Guid reviewer, CancellationToken ct)
    {
        if (reviewer == Guid.Empty || string.IsNullOrWhiteSpace(plan.Evidence) || plan.Evidence.Trim().Length > 1000
            || plan.Lines == null || plan.Lines.Count > 100 || !Money(plan.PlatformAmount)
            || plan.Lines.Any(l => l == null || l.SourceId == Guid.Empty || l.SourceKind is not ("settlement" or "earning") || !Money(l.Amount) || l.Amount <= 0)
            || plan.Lines.Select(l => (l.SourceKind, l.SourceId)).Distinct().Count() != plan.Lines.Count)
            throw new InvalidOperationException("A valid, evidenced source allocation is required.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var initial = await db.Set<PaymentOperation>().AsNoTracking().SingleOrDefaultAsync(o => o.Id == operationId, ct)
                ?? throw new InvalidOperationException("Refund operation not found.");
            var initialOrder = await db.Payments.AsNoTracking().Where(p => p.Id == initial.PaymentId).Select(p => p.OrderId).SingleAsync(ct);
            if (initialOrder == null) throw new InvalidOperationException("Refund has no attributable order.");
            // Multiple payments on one order share payable sources; lock all of them in stable order.
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM payments WHERE order_id = {initialOrder} ORDER BY id FOR UPDATE", ct);
            var operation = await db.Set<PaymentOperation>().SingleAsync(o => o.Id == operationId, ct);
            await db.Entry(operation).ReloadAsync(ct);
            var payment = await db.Payments.AsNoTracking().SingleAsync(p => p.Id == operation.PaymentId, ct);
            if (operation.Kind != "refund" || operation.State != "completed" || payment.OrderId != initialOrder
                || plan.PlatformAmount + plan.Lines.Sum(l => l.Amount) != operation.Amount)
                throw new InvalidOperationException("Only a completed refund can be allocated, and the allocation must balance exactly.");
            var canonical = JsonSerializer.Serialize(new { Evidence = plan.Evidence.Trim(), Platform = plan.PlatformAmount.ToString("F2", CultureInfo.InvariantCulture),
                Lines = plan.Lines.OrderBy(l => l.SourceKind).ThenBy(l => l.SourceId).Select(l => new { l.SourceKind, l.SourceId, Amount = l.Amount.ToString("F2", CultureInfo.InvariantCulture) }) });
            var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
            if (operation.AllocationFingerprint != null)
            {
                if (operation.AllocationFingerprint != fingerprint) throw new InvalidOperationException("Completed allocation cannot be changed; manual correction is required.");
                await tx.CommitAsync(ct); return;
            }
            var financial = await db.OrderFinancials.SingleAsync(f => f.OrderId == payment.OrderId, ct);
            if (financial.FinancialStatus == RefundConflictGuard.Status || await RefundConflictGuard.HasAsync(db, payment.OrderId, ct))
                throw new InvalidOperationException("Conflicting provider evidence requires manual review before allocation.");
            if (financial.Currency != operation.Currency) throw new InvalidOperationException("Financial currency does not match the refund.");
            var rows = new List<RefundAllocation>();
            foreach (var line in plan.Lines)
            {
                var row = new RefundAllocation { OperationId = operation.Id, SourceKind = line.SourceKind, SourceId = line.SourceId,
                    Amount = line.Amount, Currency = operation.Currency, Evidence = plan.Evidence.Trim(), ReviewedBy = reviewer };
                if (line.SourceKind == "settlement")
                {
                    var source = await db.SettlementQueue.SingleOrDefaultAsync(q => q.Id == line.SourceId, ct)
                        ?? throw new InvalidOperationException("Settlement source not found.");
                    if (source.OrderFinancialId != financial.Id || source.PayeeType is not ("supplier" or "driver") || !source.PayeeId.HasValue)
                        throw new InvalidOperationException("Settlement source does not identify an attributable payee on this order.");
                    if (!Money(source.Amount) || !Money(source.RefundAdjustment) || !Money(source.RecoveryOffset))
                        throw new InvalidOperationException("Invalid source balance requires manual review.");
                    row.PayeeType = source.PayeeType; row.PayeeId = source.PayeeId.Value;
                    row.FutureRecovery = source.Status == "paid";
                    decimal capacity;
                    if (row.FutureRecovery)
                    {
                        // A historical paid queue alone is insufficient evidence of the amount actually paid.
                        var paid = source.PayeeType == "supplier"
                            ? await db.SupplierPayouts.Where(p => p.OrderId == payment.OrderId && p.SupplierId == row.PayeeId && p.PayoutStatus == "paid" && p.Currency == row.Currency).Select(p => p.Amount).ToListAsync(ct)
                            : await db.DriverPayouts.Where(p => p.OrderId == payment.OrderId && p.DriverId == row.PayeeId && p.PayoutStatus == "paid" && p.Currency == row.Currency).Select(p => p.Amount).ToListAsync(ct);
                        if (paid.Count != 1) throw new InvalidOperationException("Paid source requires unambiguous payout evidence.");
                        capacity = paid[0];
                    }
                    else capacity = source.Amount - source.RefundAdjustment;
                    var prior = row.FutureRecovery ? await db.Set<RefundAllocation>().Where(a => a.SourceKind == line.SourceKind && a.SourceId == line.SourceId && a.FutureRecovery).SumAsync(a => a.Amount, ct) : 0m;
                    if (!Money(capacity) || row.Amount > capacity - prior) throw new InvalidOperationException("Allocation exceeds the attributable source balance.");
                    if (!row.FutureRecovery) source.RefundAdjustment += row.Amount;
                }
                else
                {
                    var source = await db.EntrepreneurEarnings.SingleOrDefaultAsync(e => e.Id == line.SourceId, ct)
                        ?? throw new InvalidOperationException("Earning source not found.");
                    if (source.OrderId != payment.OrderId || source.Currency != row.Currency || source.EntrepreneurUserId == Guid.Empty)
                        throw new InvalidOperationException("Earning source does not match the refunded order/currency.");
                    row.PayeeType = "entrepreneur"; row.PayeeId = source.EntrepreneurUserId;
                    row.FutureRecovery = source.PayoutDate != null || source.EarningStatus.Equals("PAID", StringComparison.OrdinalIgnoreCase);
                    decimal capacity;
                    if (row.FutureRecovery)
                    {
                        var batch = await db.EntrepreneurPayoutBatches.SingleOrDefaultAsync(b => b.Id == source.PayoutBatchId, ct);
                        if (batch == null || batch.Status != "PAID" || batch.EarningCount != 1 || batch.Currency != row.Currency)
                            throw new InvalidOperationException("Paid earning requires an unambiguous single-earning payout batch.");
                        capacity = batch.TotalAmount;
                    }
                    else
                    {
                        if (source.EarningStatus.ToUpperInvariant() is not ("PENDING" or "APPROVED") || source.RefundAdjustment > 0 || source.ChargebackAdjustment > 0)
                            throw new InvalidOperationException("Unresolved earning adjustments require manual review.");
                        capacity = source.EntrepreneurEarningsAmount + source.RefundAdjustment + source.ChargebackAdjustment;
                    }
                    var prior = row.FutureRecovery ? await db.Set<RefundAllocation>().Where(a => a.SourceKind == line.SourceKind && a.SourceId == line.SourceId && a.FutureRecovery).SumAsync(a => a.Amount, ct) : 0m;
                    if (!Money(capacity) || row.Amount > capacity - prior) throw new InvalidOperationException("Allocation exceeds the attributable earning balance.");
                    if (!row.FutureRecovery) { source.RefundAdjustment -= row.Amount; source.UpdatedAt = DateTime.UtcNow; }
                }
                rows.Add(row);
            }
            // Serialize claims against all concurrent future payouts for each identity/currency.
            foreach (var row in rows.OrderBy(r => r.PayeeType).ThenBy(r => r.PayeeId))
                await LockPayeeAsync(row.PayeeType, row.PayeeId, row.Currency, ct);
            rows.Add(new RefundAllocation { OperationId = operation.Id, SourceKind = "platform", Amount = plan.PlatformAmount,
                Currency = operation.Currency, Evidence = plan.Evidence.Trim(), ReviewedBy = reviewer });
            db.AddRange(rows);
            operation.AllocationFingerprint = fingerprint;
            // Allocation is not tax, dispute or referral reconciliation; original-order holds stay intact.
            financial.FinancialStatus = "refund_allocated_review";
            db.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), OrderId = payment.OrderId.Value,
                Action = "Attributed refund allocation recorded: " + operation.Id, PerformedBy = reviewer.ToString(), CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        finally { db.ChangeTracker.Clear(); }
    }

    // Caller owns a transaction and the payee advisory lock until payout + offsets commit together.
    public async Task<decimal> RecoverAsync(string type, Guid payeeId, string currency, string targetKind,
        Guid targetId, decimal payable, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null || !Money(payable) || payeeId == Guid.Empty)
            throw new InvalidOperationException("Recovery requires a valid payable and payout transaction.");
        await LockPayeeAsync(type, payeeId, currency, ct);
        if (await db.Set<PayoutRecoveryOffset>().AnyAsync(o => o.TargetKind == targetKind && o.TargetId == targetId, ct))
            throw new InvalidOperationException("This payout already has recovery offsets.");
        var claims = await db.Set<RefundAllocation>().Where(a => a.FutureRecovery && a.PayeeType == type && a.PayeeId == payeeId
            && a.Currency == currency && a.RecoveredAmount < a.Amount).OrderBy(a => a.CreatedAt).ThenBy(a => a.Id).ToListAsync(ct);
        // Preflight every matching claim before creating any offset or consuming debt.
        foreach (var claim in claims)
        {
            var operation = await db.Set<PaymentOperation>().AsNoTracking().SingleAsync(x => x.Id == claim.OperationId, ct);
            var payment = await db.Payments.AsNoTracking().SingleAsync(x => x.Id == operation.PaymentId, ct);
            if (payment.OrderId == null || !await db.OrderFinancials.AnyAsync(f => f.OrderId == payment.OrderId, ct))
                throw new InvalidOperationException("Recovery is held for missing or conflicting source financial evidence.");
            var conflict = await db.OrderFinancials.AnyAsync(f => f.OrderId == payment.OrderId && f.FinancialStatus == RefundConflictGuard.Status, ct)
                || await RefundConflictGuard.HasAsync(db, payment.OrderId, ct);
            if (conflict && !await FinancialRecoveryReleaseService.EffectiveAsync(db, claim.Id, payment.OrderId.Value, ct))
                throw new InvalidOperationException("Recovery is held for missing or conflicting source financial evidence.");
        }
        var recovered = 0m;
        foreach (var claim in claims)
        {
            await db.Entry(claim).ReloadAsync(ct);
            var offset = Math.Min(payable - recovered, claim.Amount - claim.RecoveredAmount);
            if (offset <= 0) break;
            claim.RecoveredAmount += offset; recovered += offset;
            db.Set<PayoutRecoveryOffset>().Add(new PayoutRecoveryOffset { AllocationId = claim.Id, TargetKind = targetKind, TargetId = targetId, Amount = offset });
        }
        return recovered;
    }
}
