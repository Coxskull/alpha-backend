using Alpha.API.Data;
using Alpha.API.DTOs;
using Alpha.API.Models;
using Alpha.API.Models.Entrepreneur;
using Microsoft.EntityFrameworkCore;
namespace Alpha.API.Services.Entrepreneur;

// Records a reviewed net payout and internal offsets; never initiates a transfer.
public sealed class EntrepreneurPayoutService(AppDbContext db)
{
    public Task<object> QuoteAsync(Guid id, CancellationToken ct)
        => RecordAsync(id, null, Guid.Empty, ct);
    public Task<object> MarkPaidAsync(Guid id, string reference, Guid admin, decimal? paidAmount, string currency, CancellationToken ct = default)
        => RecordAsync(id, new PayoutConfirmationDto { PayoutReference = reference, PaidAmount = paidAmount, Currency = currency }, admin, ct);

    private async Task<object> RecordAsync(Guid id, PayoutConfirmationDto? confirmation, Guid admin, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var initial = await db.EntrepreneurEarnings.AsNoTracking().SingleAsync(e => e.Id == id, ct);
            await PayoutAccountingService.LockOrderAsync(db, initial.OrderId, ct);
            var recovery = new RefundAllocationService(db);
            await recovery.LockPayeeAsync("entrepreneur", initial.EntrepreneurUserId, initial.Currency, ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM entrepreneur_earnings WHERE id = {id} FOR UPDATE", ct);
            var earning = await db.EntrepreneurEarnings.SingleAsync(e => e.Id == id, ct); await db.Entry(earning).ReloadAsync(ct);
            if (earning.EarningStatus.Equals("PAID", StringComparison.OrdinalIgnoreCase))
            {
                var batch = await db.EntrepreneurPayoutBatches.SingleAsync(b => b.Id == earning.PayoutBatchId, ct);
                if (confirmation != null && (confirmation.PaidAmount != batch.TotalAmount || confirmation.Currency != batch.Currency || confirmation.PayoutReference != batch.PayoutReference))
                    throw PayoutOperatorContract.Error("payout_replay_conflict", "Replay does not match the recorded payout.");
                await tx.CommitAsync(ct); return new { earningId = id, paidAmount = batch.TotalAmount, batch.Currency, status = "PAID" };
            }
            if (!earning.EarningStatus.Equals("APPROVED", StringComparison.OrdinalIgnoreCase) || earning.RefundAdjustment > 0 || earning.ChargebackAdjustment > 0)
                throw new InvalidOperationException("Only approved earnings with resolved adjustments can be paid.");
            var payable = earning.EntrepreneurEarningsAmount + earning.RefundAdjustment + earning.ChargebackAdjustment;
            if (!RefundAllocationService.Money(payable)) throw new InvalidOperationException("Invalid earning balance requires review.");
            var outstanding = await PayoutOperatorContract.OutstandingAsync(db, "entrepreneur", earning.EntrepreneurUserId, earning.Currency, ct);
            var offset = await recovery.RecoverAsync("entrepreneur", earning.EntrepreneurUserId, earning.Currency, "earning", id, payable, ct);
            var net = payable - offset;
            var minimum = await db.EntrepreneurProgramConfigurations.AsNoTracking().Select(c => c.MinimumPayoutThreshold).FirstOrDefaultAsync(ct);
            var result = new { earningId = id, grossAmount = earning.EntrepreneurEarningsAmount, refundAdjustment = earning.RefundAdjustment,
                chargebackAdjustment = earning.ChargebackAdjustment, recoveryOffset = offset, payableBeforeRecovery = payable, paidAmount = net, netAmount = net, remainingRecoveryBalance = outstanding - offset,
                zeroNet = net == 0, quoteIsAdvisory = confirmation == null, confirmationRequired = confirmation == null,
                minimumPayoutThreshold = minimum, eligibleForConfirmation = confirmation == null && (net == 0 || net >= minimum),
                blockedReasonCode = net > 0 && net < minimum ? "payout_threshold_not_met" : (string?)null,
                earning.Currency, status = confirmation == null ? "quote" : "PAID" };
            if (confirmation == null) { await tx.RollbackAsync(ct); return result; }
            if (admin == Guid.Empty || confirmation.PaidAmount == null || string.IsNullOrWhiteSpace(confirmation.Currency)
                || string.IsNullOrWhiteSpace(confirmation.PayoutReference) || confirmation.PayoutReference.Length > 128)
                throw PayoutOperatorContract.Error("invalid_confirmation", "Supply the net amount, currency and payout reference.");
            if (confirmation.PaidAmount != net || confirmation.Currency != earning.Currency)
                throw PayoutOperatorContract.Error("payout_quote_changed", "Refresh the quote and confirm its current net amount and currency.");
            if (net > 0 && net < minimum) throw PayoutOperatorContract.Error("payout_threshold_not_met", "Net payout is below the minimum payout threshold.");
            var batchRecord = new EntrepreneurPayoutBatch { Id = Guid.NewGuid(), SettlementDate = DateTime.UtcNow, Currency = earning.Currency,
                TotalAmount = net, EarningCount = 1, Status = "PAID", PayoutReference = confirmation.PayoutReference,
                CreatedAt = DateTime.UtcNow, PaidAt = DateTime.UtcNow, ProcessedByUserId = admin };
            db.EntrepreneurPayoutBatches.Add(batchRecord);
            earning.PayoutBatchId = batchRecord.Id; earning.PayoutDate = DateTime.UtcNow; earning.PayoutReference = confirmation.PayoutReference;
            earning.EarningStatus = "PAID"; earning.UpdatedAt = DateTime.UtcNow;
            db.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), OrderId = earning.OrderId,
                Action = $"Entrepreneur payout recorded {id}; net={net}; recovery={offset}; reference={confirmation.PayoutReference}",
                PerformedBy = admin.ToString(), CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return result;
        }
        finally { db.ChangeTracker.Clear(); }
    }
}
