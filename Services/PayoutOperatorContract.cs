using Alpha.API.Data;
using Alpha.API.Models;
using Microsoft.EntityFrameworkCore;
namespace Alpha.API.Services;

public static class PayoutOperatorContract
{
    // Caller holds the existing payee lock; calculate before applying provisional offsets.
    public static Task<decimal> OutstandingAsync(AppDbContext db, string type, Guid payee, string currency, CancellationToken ct)
        => db.Set<RefundAllocation>().Where(x => x.FutureRecovery && x.PayeeType == type && x.PayeeId == payee
            && x.Currency == currency && x.RecoveredAmount < x.Amount).SumAsync(x => x.Amount - x.RecoveredAmount, ct);
    public static InvalidOperationException Error(string code, string message)
    {
        var error = new InvalidOperationException(message); error.Data["PayoutCode"] = code; return error;
    }
    public static string Code(InvalidOperationException error)
        => error.Data["PayoutCode"] as string ?? "payout_review_required";
}
