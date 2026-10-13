using System.Data;
using System.Text.RegularExpressions;
using Alpha.API.Data;
using Microsoft.EntityFrameworkCore;
namespace Alpha.API.Services;
public sealed class PayoutRecordInventoryRow
{
    public Guid Id {get;set;}
    public Guid? OrderId {get;set;}
    public Guid? PayeeId {get;set;}
    public string? Amount {get;set;}
    public bool AmountValid {get;set;}
    public string Currency {get;set;}="";
    public string Status {get;set;}="";
    public string? Reference {get;set;}
    public long SourceCount {get;set;}
    public int ExpectedSourceCount {get;set;}
    public long PaidCurrencySourceCount {get;set;}
}
// Record-first exception inventory only. Never links a record, repairs money, or clears a hold.
public sealed class PayoutRecordInventoryService(AppDbContext db)
{
    public async Task<object> ReadAsync(string kind, Guid? after, int limit, Guid actor, CancellationToken ct)
    {
        if(kind is not ("supplier" or "driver" or "batch") || limit<1 || limit>20) throw new ArgumentException("Choose supplier, driver or batch and a limit from1 to20.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,ct);
        await new FinancialConflictReviewService(db).AdministratorAsync(actor,ct);
        var rows=kind switch
        {
            "supplier"=>await db.Database.SqlQuery<PayoutRecordInventoryRow>($$"""
                SELECT * FROM (
                  SELECT p.id AS "Id", p.order_id AS "OrderId", p.supplier_id AS "PayeeId", CASE WHEN (p.amount >= 0 AND p.amount <= 9999999999999999.99 AND ROUND(p.amount,2) = p.amount) THEN TRIM_SCALE(p.amount)::text ELSE NULL::text END AS "Amount", (p.amount >= 0 AND p.amount <= 9999999999999999.99 AND ROUND(p.amount,2) = p.amount) AS "AmountValid", p.currency AS "Currency", p.payout_status AS "Status", NULL::text AS "Reference",
                    (SELECT COUNT(*) FROM settlement_queue q JOIN order_financials f ON f.id=q.order_financial_id WHERE f.order_id=p.order_id AND q.payee_type='supplier' AND q.payee_id=p.supplier_id) AS "SourceCount",
                    1 AS "ExpectedSourceCount",
                    (SELECT COUNT(*) FROM settlement_queue q JOIN order_financials f ON f.id=q.order_financial_id WHERE f.order_id=p.order_id AND q.payee_type='supplier' AND q.payee_id=p.supplier_id AND q.status='paid' AND f.currency=p.currency) AS "PaidCurrencySourceCount"
                  FROM supplier_payouts p WHERE ({{after}}::uuid IS NULL OR p.id>{{after}}::uuid)
                ) r WHERE "SourceCount"<>"ExpectedSourceCount" OR "SourceCount"=0 OR "PaidCurrencySourceCount"<>"SourceCount" OR NOT "AmountValid" OR "Currency"!~'^[A-Z]{3}$' OR LOWER("Status")<>'paid'
                ORDER BY "Id" LIMIT {{limit+1}}
                """).ToListAsync(ct),
            "driver"=>await db.Database.SqlQuery<PayoutRecordInventoryRow>($$"""
                SELECT * FROM (
                  SELECT p.id AS "Id", p.order_id AS "OrderId", p.driver_id AS "PayeeId", CASE WHEN (p.amount >= 0 AND p.amount <= 9999999999999999.99 AND ROUND(p.amount,2) = p.amount) THEN TRIM_SCALE(p.amount)::text ELSE NULL::text END AS "Amount", (p.amount >= 0 AND p.amount <= 9999999999999999.99 AND ROUND(p.amount,2) = p.amount) AS "AmountValid", p.currency AS "Currency", p.payout_status AS "Status", NULL::text AS "Reference",
                    (SELECT COUNT(*) FROM settlement_queue q JOIN order_financials f ON f.id=q.order_financial_id WHERE f.order_id=p.order_id AND q.payee_type='driver' AND q.payee_id=p.driver_id) AS "SourceCount",
                    1 AS "ExpectedSourceCount",
                    (SELECT COUNT(*) FROM settlement_queue q JOIN order_financials f ON f.id=q.order_financial_id WHERE f.order_id=p.order_id AND q.payee_type='driver' AND q.payee_id=p.driver_id AND q.status='paid' AND f.currency=p.currency) AS "PaidCurrencySourceCount"
                  FROM driver_payouts p WHERE ({{after}}::uuid IS NULL OR p.id>{{after}}::uuid)
                ) r WHERE "SourceCount"<>"ExpectedSourceCount" OR "SourceCount"=0 OR "PaidCurrencySourceCount"<>"SourceCount" OR NOT "AmountValid" OR "Currency"!~'^[A-Z]{3}$' OR LOWER("Status")<>'paid'
                ORDER BY "Id" LIMIT {{limit+1}}
                """).ToListAsync(ct),
            _=>await db.Database.SqlQuery<PayoutRecordInventoryRow>($$"""
                SELECT * FROM (
                  SELECT p.id AS "Id", NULL::uuid AS "OrderId", NULL::uuid AS "PayeeId", CASE WHEN (p.total_amount >= 0 AND p.total_amount <= 9999999999999999.99 AND ROUND(p.total_amount,2) = p.total_amount) THEN TRIM_SCALE(p.total_amount)::text ELSE NULL::text END AS "Amount", (p.total_amount >= 0 AND p.total_amount <= 9999999999999999.99 AND ROUND(p.total_amount,2) = p.total_amount) AS "AmountValid", p.currency AS "Currency", p.status AS "Status", p.payout_reference AS "Reference",
                    (SELECT COUNT(*) FROM entrepreneur_earnings e WHERE e.payout_batch_id=p.id) AS "SourceCount", p.earning_count AS "ExpectedSourceCount",
                    (SELECT COUNT(*) FROM entrepreneur_earnings e WHERE e.payout_batch_id=p.id AND e.earning_status='PAID' AND e.currency=p.currency) AS "PaidCurrencySourceCount"
                  FROM entrepreneur_payout_batches p WHERE ({{after}}::uuid IS NULL OR p.id>{{after}}::uuid)
                ) r WHERE "SourceCount"<>"ExpectedSourceCount" OR "SourceCount"=0 OR "PaidCurrencySourceCount"<>"SourceCount" OR NOT "AmountValid" OR "Currency"!~'^[A-Z]{3}$' OR LOWER("Status")<>'paid'
                ORDER BY "Id" LIMIT {{limit+1}}
                """).ToListAsync(ct)
        };
        var more=rows.Count>limit;rows=rows.Take(limit).ToList();await tx.CommitAsync(ct);
        return new {kind,rows=rows.Select(r=>new {r.Id,r.OrderId,r.PayeeId,amount=r.Amount,amountValid=r.AmountValid,r.Currency,r.Status,r.Reference,r.SourceCount,r.ExpectedSourceCount,r.PaidCurrencySourceCount,
            issues=Issues(r)}),next=more?(Guid?)rows[^1].Id:null,scope="internal_record_link_inventory_only"};
    }
    private static string[] Issues(PayoutRecordInventoryRow row)
    {
        var issues=new List<string>();
        if(row.SourceCount==0)issues.Add("missing_payout_source");
        if(row.SourceCount!=row.ExpectedSourceCount)issues.Add("source_count_mismatch");
        if(row.PaidCurrencySourceCount!=row.SourceCount)issues.Add("source_state_or_currency_mismatch");
        if(!row.AmountValid)issues.Add("invalid_payout_amount");
        if(!Regex.IsMatch(row.Currency,"^[A-Z]{3}$"))issues.Add("invalid_currency");
        if(!row.Status.Equals("paid",StringComparison.OrdinalIgnoreCase))issues.Add("payout_record_not_paid");
        return issues.ToArray();
    }
}
