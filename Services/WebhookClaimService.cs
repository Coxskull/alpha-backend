using Alpha.API.Data;
using Alpha.API.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text.Json;
namespace Alpha.API.Services;

public sealed class WebhookClaimService(AppDbContext db)
{
    public async Task<(PaymentWebhookEvent Event, bool Claimed)> ClaimAsync(string gateway, string eventId,
        string eventType, string payload, CancellationToken cancellationToken)
    {
        var item = await db.PaymentWebhookEvents.SingleOrDefaultAsync(e => e.Gateway == gateway
            && e.GatewayEventId == eventId, cancellationToken);
        if (item == null)
        {
            item = new PaymentWebhookEvent { Id = Guid.NewGuid(), Gateway = gateway, GatewayEventId = eventId,
                EventType = eventType, Payload = JsonDocument.Parse(payload), ReceivedAt = DateTime.UtcNow };
            db.Add(item);
            try { await db.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                db.Entry(item).State = EntityState.Detached;
                item = await db.PaymentWebhookEvents.SingleAsync(e => e.Gateway == gateway && e.GatewayEventId == eventId, cancellationToken);
            }
        }
        if (item.Processed) return (item, false);
        var now = DateTime.UtcNow;
        var claimed = await db.PaymentWebhookEvents.Where(e => e.Id == item.Id && !e.Processed
            && (e.ProcessingUntil == null || e.ProcessingUntil < now)).ExecuteUpdateAsync(setters =>
                setters.SetProperty(e => e.ProcessingUntil, now.AddMinutes(10)), cancellationToken) == 1;
        await db.Entry(item).ReloadAsync(cancellationToken);
        return (item, claimed);
    }
}
