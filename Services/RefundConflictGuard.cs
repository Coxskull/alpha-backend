using Alpha.API.Data;
using Microsoft.EntityFrameworkCore;

namespace Alpha.API.Services;

public static class RefundConflictGuard
{
    public const string Status = "refund_evidence_conflict";

    public static Task<bool> HasAsync(AppDbContext db, Guid? orderId, CancellationToken ct)
        => db.Payments.AnyAsync(p => p.OrderId == orderId && p.RefundEvidenceConflict, ct);
}
