using Alpha.API.Models;
using Alpha.API.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace Alpha.API.Integration.Tests;

[Collection("Disposable PostgreSQL")]
public sealed class WebhookRecoveryTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task FailedEventCanBeReclaimedInsteadOfAcknowledgedAsProcessed()
    {
        var key = Guid.NewGuid().ToString("N");
        using var first = fixture.Context();
        var lease = await new WebhookClaimService(first).ClaimAsync("stripe", key, "test", "{}", CancellationToken.None);
        Assert.True(lease.Claimed);
        lease.Event.ProcessingUntil = null; lease.Event.ProcessingError = "synthetic_failure";
        await first.SaveChangesAsync();
        using var retry = fixture.Context();
        var reclaimed = await new WebhookClaimService(retry).ClaimAsync("stripe", key, "test", "{}", CancellationToken.None);
        Assert.True(reclaimed.Claimed); Assert.False(reclaimed.Event.Processed);
        Assert.Equal(lease.Event.Id, reclaimed.Event.Id);
        reclaimed.Event.Processed = true; reclaimed.Event.ProcessingUntil = null;
        await retry.SaveChangesAsync();
        using var done = fixture.Context();
        var completed = await new WebhookClaimService(done).ClaimAsync("stripe", key, "test", "{}", CancellationToken.None);
        Assert.False(completed.Claimed); Assert.True(completed.Event.Processed);
    }

    [Fact]
    public async Task ConcurrentDeliveriesHaveOnlyOneProcessingLease()
    {
        var key = Guid.NewGuid().ToString("N");
        async Task<bool> Claim()
        {
            using var db = fixture.Context();
            return (await new WebhookClaimService(db).ClaimAsync("paymongo", key, "test", "{}", CancellationToken.None)).Claimed;
        }
        var claims = await Task.WhenAll(Claim(), Claim());
        Assert.Single(claims, claimed => claimed);
        using var verify = fixture.Context();
        Assert.Single(await verify.PaymentWebhookEvents.Where(e => e.Gateway == "paymongo" && e.GatewayEventId == key).ToListAsync());
    }

    [Fact]
    public async Task ExpiredLeaseIsRecoverableAfterWorkerCrash()
    {
        var key = Guid.NewGuid().ToString("N");
        using var db = fixture.Context();
        db.PaymentWebhookEvents.Add(new PaymentWebhookEvent { Id = Guid.NewGuid(), Gateway = "stripe", GatewayEventId = key,
            EventType = "test", Payload = System.Text.Json.JsonDocument.Parse("{}"), ReceivedAt = DateTime.UtcNow,
            ProcessingUntil = DateTime.UtcNow.AddMinutes(-1) });
        await db.SaveChangesAsync();
        var recovered = await new WebhookClaimService(db).ClaimAsync("stripe", key, "test", "{}", CancellationToken.None);
        Assert.True(recovered.Claimed);
    }
}
