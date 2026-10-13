using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Alpha.API.Data;
using Alpha.API.Models;
using Microsoft.EntityFrameworkCore;
namespace Alpha.API.Services;

public sealed record ReconciliationSnapshot(string Kind, Guid Id, Guid OrderId, string PayeeType, Guid? PayeeId,
    string Currency, string Status, decimal? GrossAmount, decimal? Adjustment, decimal? RecoveryOffset, decimal? ExpectedNet,
    decimal? RecordedNet, string? Reference, string[] Issues, string Fingerprint)
{
    public bool InspectionAvailable {get; init;} = true;
}
public sealed record ReconciliationCapture(Guid Id, Guid Actor, DateTime CapturedAt, ReconciliationSnapshot Snapshot);
public sealed record ReconciliationCaptureRequest(Guid Id, string Fingerprint);
public sealed class ReconciliationQueueRow
{
    public Guid Id { get; set; }
    public string Status { get; set; } = "";
}

// Internal consistency only: no provider, money, claim, hold, or payout mutation.
public sealed class PayoutReconciliationService(AppDbContext db)
{
    private const string Prefix = "Payout reconciliation v1;";
    private static void Kind(string kind) { if (kind is not ("settlement" or "earning")) throw new ArgumentException("Unsupported reconciliation kind."); }
    private Task Admin(Guid actor, CancellationToken ct) => new FinancialConflictReviewService(db).AdministratorAsync(actor, ct);
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)))).ToLowerInvariant();

    public async Task<object> QueueAsync(string kind, Guid? after, int limit, Guid actor, CancellationToken ct)
    {
        Kind(kind); if (limit < 1 || limit > 20) throw new ArgumentException("Limit must be between 1 and 20.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await Admin(actor, ct);
        // Keyset pagination uses the existing primary-key index, with no deep OFFSET.
        var rows = kind == "settlement"
            ? await db.Database.SqlQuery<ReconciliationQueueRow>($"SELECT id AS \"Id\", status AS \"Status\" FROM settlement_queue WHERE payee_type IN ('supplier','driver') AND ({after}::uuid IS NULL OR id > {after}::uuid) ORDER BY id LIMIT {limit + 1}").ToListAsync(ct)
            : await db.Database.SqlQuery<ReconciliationQueueRow>($"SELECT id AS \"Id\", earning_status AS \"Status\" FROM entrepreneur_earnings WHERE ({after}::uuid IS NULL OR id > {after}::uuid) ORDER BY id LIMIT {limit + 1}").ToListAsync(ct);
        var hasMore = rows.Count > limit; rows = rows.Take(limit).ToList();
        var snapshots = new List<ReconciliationSnapshot>();
        foreach(var row in rows)
        {
            try { snapshots.Add(await Snapshot(kind,row.Id,ct)); }
            catch(InvalidOperationException ex) when(ex.Data["ReconciliationScopeExceeded"] is true)
            {
                // A bounded manual-review marker is not evidence or a zero-value payout.
                snapshots.Add(new(kind,row.Id,Guid.Empty,"unknown",null,"",row.Status,null,null,null,null,null,null,
                    ["manual_review_scope_exceeded"],Hash(new {kind,row.Id,scope="manual_review_scope_exceeded"})) {InspectionAvailable=false});
            }
        }
        await tx.CommitAsync(ct);
        return new { kind, rows=snapshots, next = hasMore ? (Guid?)rows[^1].Id : null, scope = "internal_records_only" };
    }

    public async Task<ReconciliationSnapshot> InspectAsync(string kind, Guid id, Guid actor, CancellationToken ct)
    {
        Kind(kind); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await Admin(actor, ct); var result = await Snapshot(kind, id, ct); await tx.CommitAsync(ct); return result;
    }

    private static InvalidOperationException ScopeExceeded(string message)
    {
        var error=new InvalidOperationException(message);error.Data["ReconciliationScopeExceeded"]=true;return error;
    }

    private async Task<ReconciliationSnapshot> Snapshot(string kind, Guid id, CancellationToken ct)
    {
        var issues = new List<string>(); object source; object records; Guid order; Guid? payee; string type, currency, status;
        decimal gross, adjustment, declaredOffset = 0; decimal? recorded = null; string? reference = null;
        if (kind == "settlement")
        {
            var q = await db.SettlementQueue.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new KeyNotFoundException("Payout source not found.");
            var f = await db.OrderFinancials.AsNoTracking().SingleOrDefaultAsync(x => x.Id == q.OrderFinancialId, ct);
            order = f?.OrderId ?? Guid.Empty; currency = f?.Currency ?? ""; payee = q.PayeeId; type = q.PayeeType; status = q.Status;
            gross = q.Amount; adjustment = -q.RefundAdjustment; declaredOffset = q.RecoveryOffset; source = new { q, f };
            if (type is not ("supplier" or "driver")) throw new ArgumentException("Unsupported settlement payee type.");
            if (order == Guid.Empty || !payee.HasValue) issues.Add("missing_source_attribution");
            var suppliers = await db.SupplierPayouts.AsNoTracking().Where(x => x.OrderId == order).OrderBy(x => x.Id).Take(201).ToListAsync(ct);
            var drivers = await db.DriverPayouts.AsNoTracking().Where(x => x.OrderId == order).OrderBy(x => x.Id).Take(201).ToListAsync(ct);
            if(suppliers.Count > 200 || drivers.Count > 200) throw ScopeExceeded("Order exceeds bounded reconciliation scope; manual review required.");
            var matches = type == "supplier" ? suppliers.Where(x => x.SupplierId == payee).Select(x => new { x.Amount, x.Currency, x.PayoutStatus, x.PaidAt }).ToList()
                : drivers.Where(x => x.DriverId == payee).Select(x => new { x.Amount, x.Currency, x.PayoutStatus, x.PaidAt }).ToList();
            var sameSources = await db.SettlementQueue.CountAsync(x => x.OrderFinancialId == q.OrderFinancialId && x.PayeeType == type && x.PayeeId == payee, ct);
            if(sameSources != 1) issues.Add("ambiguous_payout_source");
            records = new { suppliers, drivers, sameSources };
            if (status == "paid")
            {
                if (matches.Count != 1) issues.Add("missing_or_duplicate_payout");
                else { var p = matches[0]; recorded = p.Amount; if (p.Currency != currency) issues.Add("payout_currency_mismatch"); if(p.PayoutStatus != "paid" || p.PaidAt == null) issues.Add("incomplete_paid_metadata"); }
            }
            else if(matches.Count > 0) issues.Add("payout_without_paid_source");
            if(q.RefundAdjustment < 0) issues.Add("invalid_adjustment");
        }
        else
        {
            var e = await db.EntrepreneurEarnings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new KeyNotFoundException("Payout source not found.");
            order = e.OrderId; payee = e.EntrepreneurUserId; type = "entrepreneur"; currency = e.Currency; status = e.EarningStatus;
            gross = e.EntrepreneurEarningsAmount; adjustment = e.RefundAdjustment + e.ChargebackAdjustment; source = e;
            var batch = await db.EntrepreneurPayoutBatches.AsNoTracking().SingleOrDefaultAsync(x => x.Id == e.PayoutBatchId, ct);
            var members = await db.EntrepreneurEarnings.AsNoTracking().Where(x => e.PayoutBatchId != null && x.PayoutBatchId == e.PayoutBatchId).OrderBy(x => x.Id).Take(201).Select(x => new {x.Id,x.EntrepreneurUserId,x.Currency,x.EarningStatus}).ToListAsync(ct);
            if(members.Count > 200) throw ScopeExceeded("Batch exceeds bounded reconciliation scope; manual review required.");
            records = new { batch, members };
            if(status == "PAID")
            {
                if(batch == null) issues.Add("missing_payout_batch");
                else { recorded = batch.TotalAmount; reference = batch.PayoutReference;
                    if(batch.Currency != currency || members.Any(x => x.Currency != currency)) issues.Add("payout_currency_mismatch");
                    if(members.Count != 1 || members.Any(x => x.EntrepreneurUserId != payee) || batch.EarningCount != 1) issues.Add("unsupported_or_mixed_batch");
                    if(batch.Status != "PAID" || batch.PaidAt == null || batch.ProcessedByUserId == null || e.PayoutDate == null) issues.Add("incomplete_paid_metadata");
                    if(e.PayoutReference != reference) issues.Add("reference_mismatch"); }
            }
            else if(batch != null || e.PayoutBatchId != null) issues.Add("payout_without_paid_source");
            if(e.RefundAdjustment > 0 || e.ChargebackAdjustment > 0) issues.Add("invalid_adjustment");
        }
        var orderRow = await db.Orders.AsNoTracking().Where(x => x.Id == order).Select(x => new {x.Id,x.Currency}).SingleOrDefaultAsync(ct);
        if(orderRow == null || orderRow.Currency != currency || payee == null || payee == Guid.Empty) issues.Add("missing_or_mismatched_order");
        var offsets = await db.Set<PayoutRecoveryOffset>().AsNoTracking().Where(x => x.TargetId == id).OrderBy(x => x.Id).Take(201).ToListAsync(ct);
        if(offsets.Count > 200) throw ScopeExceeded("Offsets exceed bounded reconciliation scope; manual review required.");
        var allocationIds = offsets.Select(x => x.AllocationId).ToArray();
        var claims = await db.Set<RefundAllocation>().AsNoTracking().Where(x => allocationIds.Contains(x.Id)).OrderBy(x => x.Id).ToListAsync(ct);
        var totals = await db.Set<PayoutRecoveryOffset>().Where(x => allocationIds.Contains(x.AllocationId)).GroupBy(x => x.AllocationId).Select(g => new {Id=g.Key, Amount=g.Sum(x => x.Amount)}).OrderBy(x => x.Id).ToListAsync(ct);
        foreach(var offset in offsets)
        {
            var a = claims.SingleOrDefault(x => x.Id == offset.AllocationId);
            if(offset.TargetKind != kind || !RefundAllocationService.Money(offset.Amount) || offset.Amount == 0 || a == null || !a.FutureRecovery
                || a.PayeeType != type || a.PayeeId != payee || a.Currency != currency) issues.Add("recovery_attribution_mismatch");
        }
        foreach(var a in claims) if(a.RecoveredAmount != totals.Single(x => x.Id == a.Id).Amount || a.RecoveredAmount > a.Amount || a.RecoveredAmount < 0) issues.Add("recovery_ledger_mismatch");
        var recovered = offsets.Sum(x => x.Amount);
        if(kind == "settlement" && declaredOffset != recovered) issues.Add("recovery_offset_mismatch");
        var expected = gross + adjustment - recovered;
        if(!RefundAllocationService.Money(gross) || !RefundAllocationService.Money(expected)) issues.Add("invalid_source_balance");
        var paid = status.Equals("paid", StringComparison.OrdinalIgnoreCase);
        if(paid && recorded != expected) issues.Add("net_amount_mismatch");
        if(!paid && offsets.Count > 0) issues.Add("offset_without_paid_source");
        var auditPrefix = kind == "settlement" ? $"Payout recorded {id};" : $"Entrepreneur payout recorded {id};";
        var audits = await db.AuditLogs.AsNoTracking().Where(x => x.OrderId == order && x.Action.StartsWith(auditPrefix)).OrderBy(x => x.Id).Take(201).ToListAsync(ct);
        if(audits.Count > 200) throw ScopeExceeded("Audit exceeds bounded reconciliation scope; manual review required.");
        if(paid)
        {
            if(audits.Count != 1) issues.Add("missing_or_duplicate_payout_audit");
            else { var action=audits[0].Action; var marker=action.IndexOf("; reference=",StringComparison.Ordinal);
                var auditReference=marker < 0 ? null : action[(marker+12)..];
                if(string.IsNullOrWhiteSpace(auditReference) || string.IsNullOrWhiteSpace(audits[0].PerformedBy)) issues.Add("missing_payout_reference");
                var expectedAudit = kind == "settlement" ? $"Payout recorded {id}; net={expected}; recovery={recovered}; reference=" : $"Entrepreneur payout recorded {id}; net={expected}; recovery={recovered}; reference=";
                if(!action.StartsWith(expectedAudit,StringComparison.Ordinal)) issues.Add("payout_audit_balance_mismatch");
                if(kind == "settlement") reference=auditReference;
                else if(reference != auditReference) issues.Add("reference_mismatch"); }
        }
        else if(audits.Count > 0) issues.Add("audit_without_paid_source");
        var distinct = issues.Distinct().OrderBy(x => x).ToArray();
        var facts = new {kind,id,source,records,orderRow,offsets,claims,totals,audits,distinct};
        return new(kind,id,order,type,payee,currency,status,gross,adjustment,recovered,expected,recorded,reference,distinct,Hash(facts));
    }

    public async Task<ReconciliationCapture> CaptureAsync(string kind, Guid id, ReconciliationCaptureRequest request, Guid actor, CancellationToken ct)
    {
        Kind(kind); if(request.Id == Guid.Empty || request.Fingerprint?.Length != 64 || request.Fingerprint.Any(c => !Uri.IsHexDigit(c))) throw new ArgumentException("A capture ID and current fingerprint are required.");
        await Admin(actor,ct);
        // Serializes only capture replays; payout operations remain independent. Repeatable-read gives one coherent observation.
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_lock(hashtextextended({request.Id.ToString()}, 4))",ct);
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,ct);
            await Admin(actor,ct);
            var existing = await db.AuditLogs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.Id,ct);
            if(existing != null)
            {
                if(!existing.Action.StartsWith(Prefix,StringComparison.Ordinal)) throw new InvalidOperationException("Capture ID conflicts with another audit record.");
                var saved = JsonSerializer.Deserialize<ReconciliationCapture>(existing.Action[Prefix.Length..]) ?? throw new InvalidOperationException("Invalid capture history requires review.");
                if(saved.Actor != actor || saved.Snapshot.Kind != kind || saved.Snapshot.Id != id || saved.Snapshot.Fingerprint != request.Fingerprint) throw new InvalidOperationException("Capture replay does not match original evidence.");
                await tx.CommitAsync(ct); return saved;
            }
            await Admin(actor,ct); var snapshot = await Snapshot(kind,id,ct);
            if(snapshot.Fingerprint != request.Fingerprint) throw new InvalidOperationException("Reconciliation evidence changed. Refresh before capturing.");
            if(snapshot.OrderId == Guid.Empty) throw new InvalidOperationException("Missing order attribution prevents an audit capture.");
            var capture = new ReconciliationCapture(request.Id,actor,DateTime.UtcNow,snapshot);
            db.AuditLogs.Add(new AuditLog {Id=request.Id,OrderId=snapshot.OrderId,Action=Prefix+JsonSerializer.Serialize(capture),PerformedBy=actor.ToString(),CreatedAt=capture.CapturedAt});
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return capture;
        }
        finally
        {
            try { await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_unlock(hashtextextended({request.Id.ToString()}, 4))",CancellationToken.None); }
            finally { db.ChangeTracker.Clear(); await db.Database.CloseConnectionAsync(); }
        }
    }

    public async Task<object> HistoryAsync(string kind, Guid id, Guid? after, int limit, Guid actor, CancellationToken ct)
    {
        Kind(kind); if(limit < 1 || limit > 20) throw new ArgumentException("Limit must be between 1 and 20."); await Admin(actor,ct);
        // Exact source scope is carried in immutable payload; server filters before pagination.
        var marker = Prefix+"{\"Id\":"; // JSON field names are versioned and use default serializer casing.
        var rows = await db.AuditLogs.AsNoTracking().Where(x => x.Action.StartsWith(marker)
            && x.Action.Contains("\"Kind\":\""+kind+"\",\"Id\":\""+id+"\"") && (after == null || x.Id.CompareTo(after.Value) > 0))
            .OrderBy(x => x.Id).Take(limit+1).ToListAsync(ct);
        var more=rows.Count > limit; rows=rows.Take(limit).ToList();
        return new {rows=rows.Select(x => JsonSerializer.Deserialize<ReconciliationCapture>(x.Action[Prefix.Length..])).ToArray(), next=more?(Guid?)rows[^1].Id:null};
    }
}
