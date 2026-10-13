using Alpha.API.Models;
using Alpha.API.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text.Json;
using Xunit;
namespace Alpha.API.Integration.Tests;
public sealed partial class RefundRecoveryTests
{
    private async Task<Guid> ReconciliationAdmin(string role="admin", bool active=true)
    {
        using var db=fixture.Context(); var id=Guid.NewGuid(); db.Users.Add(new User {Id=id,FullName="Synthetic reconciliation operator",Email=id+"@example.invalid",Role=role,IsActive=active}); await db.SaveChangesAsync(); return id;
    }
    [Fact]
    public async Task ReconciliationReadsBalancedSettlementWithoutChangingFinancials()
    {
        var s=await Settlement(paid:false); await Pay(s.Queue,80); var actor=await ReconciliationAdmin();
        using var db=fixture.Context(); var service=new PayoutReconciliationService(db); var before=await db.AuditLogs.CountAsync();
        var result=await service.InspectAsync("settlement",s.Queue,actor,Ct); Assert.Empty(result.Issues); Assert.Equal(80,result.RecordedNet); Assert.Equal("synthetic-reviewed-payment",result.Reference);
        Assert.Equal(before,await db.AuditLogs.CountAsync()); Assert.Equal(result.Fingerprint,(await service.InspectAsync("settlement",s.Queue,actor,Ct)).Fingerprint);
        var queue=JsonSerializer.SerializeToElement(await service.QueueAsync("settlement",null,20,actor,Ct)); Assert.True(queue.GetProperty("rows").GetArrayLength() <= 20);
    }
    [Fact]
    public async Task ReconciliationFlagsCurrencyAndPayeeMismatchWithoutNettingOtherRecords()
    {
        var s=await Settlement(); var actor=await ReconciliationAdmin(); using var db=fixture.Context();
        await db.SupplierPayouts.Where(x => x.OrderId==s.Order).ExecuteUpdateAsync(x => x.SetProperty(p=>p.Currency,"EUR"));
        var result=await new PayoutReconciliationService(db).InspectAsync("settlement",s.Queue,actor,Ct); Assert.Contains("payout_currency_mismatch",result.Issues); Assert.Contains("missing_or_duplicate_payout_audit",result.Issues);
        var other=Guid.NewGuid();db.Suppliers.Add(new Supplier {Id=other,Name="Synthetic unrelated supplier"});await db.SaveChangesAsync();
        await db.SupplierPayouts.Where(x => x.OrderId==s.Order).ExecuteUpdateAsync(x => x.SetProperty(p=>p.SupplierId,other));
        result=await new PayoutReconciliationService(db).InspectAsync("settlement",s.Queue,actor,Ct); Assert.Null(result.RecordedNet); Assert.Contains("missing_or_duplicate_payout",result.Issues);
    }
    [Fact]
    public async Task ReconciliationZeroNetChecksRecoveryLedgerAndCaptureIsImmutable()
    {
        var source=await Settlement(); var refund=await Refund(source.Payment,30); await Allocate(refund,Plan(source.Queue,30));
        var s=await Settlement(paid:false,amount:20,payee:source.Payee);await Pay(s.Queue,0);var actor=await ReconciliationAdmin();
        using var db=fixture.Context();var service=new PayoutReconciliationService(db);var result=await service.InspectAsync("settlement",s.Queue,actor,Ct);
        Assert.Empty(result.Issues);Assert.Equal(20,result.RecoveryOffset);Assert.Equal(0,result.RecordedNet);
        var request=new ReconciliationCaptureRequest(Guid.NewGuid(),result.Fingerprint);var capture=await service.CaptureAsync("settlement",s.Queue,request,actor,Ct);
        Assert.Equal(JsonSerializer.Serialize(capture),JsonSerializer.Serialize(await service.CaptureAsync("settlement",s.Queue,request,actor,Ct)));
        var history=JsonSerializer.SerializeToElement(await service.HistoryAsync("settlement",s.Queue,null,20,actor,Ct));Assert.Single(history.GetProperty("rows").EnumerateArray());
        await Assert.ThrowsAsync<PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE audit_logs SET action = 'changed' WHERE id = {capture.Id}"));
        await Assert.ThrowsAsync<PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM audit_logs WHERE id = {capture.Id}"));
        Assert.Equal(20,await db.Set<PayoutRecoveryOffset>().Where(x=>x.TargetId==s.Queue).SumAsync(x=>x.Amount));
    }
    [Fact]
    public async Task ConcurrentCaptureReplaysHaveOneEffectAndRejectOtherActor()
    {
        var s=await Settlement(paid:false);await Pay(s.Queue,80);var actor=await ReconciliationAdmin();var other=await ReconciliationAdmin();string fingerprint;
        using(var db=fixture.Context())fingerprint=(await new PayoutReconciliationService(db).InspectAsync("settlement",s.Queue,actor,Ct)).Fingerprint;
        var request=new ReconciliationCaptureRequest(Guid.NewGuid(),fingerprint);
        async Task<Guid> Capture(){using var db=fixture.Context();return (await new PayoutReconciliationService(db).CaptureAsync("settlement",s.Queue,request,actor,Ct)).Id;}
        var ids=await Task.WhenAll(Capture(),Capture());Assert.Equal(ids[0],ids[1]);using var check=fixture.Context();Assert.Equal(1,await check.AuditLogs.CountAsync(x=>x.Id==request.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>new PayoutReconciliationService(check).CaptureAsync("settlement",s.Queue,request,other,Ct));
    }
    [Fact]
    public async Task StaleCaptureRejectedAndPendingPayoutSnapshotDoesNotRaceOrConsumeRecovery()
    {
        var s=await Settlement(paid:false);var actor=await ReconciliationAdmin();using var db=fixture.Context();var service=new PayoutReconciliationService(db);
        var before=await service.InspectAsync("settlement",s.Queue,actor,Ct);await Pay(s.Queue,80);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.CaptureAsync("settlement",s.Queue,new(Guid.NewGuid(),before.Fingerprint),actor,Ct));
        var after=await service.InspectAsync("settlement",s.Queue,actor,Ct);Assert.NotEqual(before.Fingerprint,after.Fingerprint);Assert.Empty(after.Issues);
    }
    [Theory]
    [InlineData("dispatcher",true)] [InlineData("supplier",true)] [InlineData("admin",false)]
    public async Task ReconciliationRequiresActiveAdministrator(string role,bool active)
    {
        var s=await Settlement();var actor=await ReconciliationAdmin(role,active);using var db=fixture.Context();var service=new PayoutReconciliationService(db);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.InspectAsync("settlement",s.Queue,actor,Ct));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.QueueAsync("earning",null,20,actor,Ct));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.CaptureAsync("settlement",s.Queue,new(Guid.NewGuid(),new string('a',64)),actor,Ct));
    }
    [Fact]
    public async Task EntrepreneurReconciliationRejectsForeignCurrencyBatchAndUsesSignedAdjustments()
    {
        var e=await Earning(false);var actor=await ReconciliationAdmin();using var db=fixture.Context();
        await new Alpha.API.Services.Entrepreneur.EntrepreneurPayoutService(db).MarkPaidAsync(e.Earning,"synthetic-ref",actor,40,"USD",Ct);
        var service=new PayoutReconciliationService(db);var result=await service.InspectAsync("earning",e.Earning,actor,Ct);Assert.Empty(result.Issues);Assert.Equal(40,result.RecordedNet);
        var batch=await db.EntrepreneurEarnings.Where(x=>x.Id==e.Earning).Select(x=>x.PayoutBatchId).SingleAsync();
        await db.EntrepreneurPayoutBatches.Where(x=>x.Id==batch).ExecuteUpdateAsync(x=>x.SetProperty(b=>b.Currency,"EUR"));
        Assert.Contains("payout_currency_mismatch",(await service.InspectAsync("earning",e.Earning,actor,Ct)).Issues);
    }
    [Fact]
    public async Task ReconciliationConcurrentWithPayoutObservesOneCommittedState()
    {
        var s=await Settlement(paid:false);var actor=await ReconciliationAdmin();
        async Task<ReconciliationSnapshot> Inspect(){using var db=fixture.Context();return await new PayoutReconciliationService(db).InspectAsync("settlement",s.Queue,actor,Ct);}
        var inspect=Inspect();var pay=Pay(s.Queue,80);await Task.WhenAll(inspect,pay);var observed=await inspect;
        Assert.Empty(observed.Issues);
        if(observed.Status=="paid")Assert.Equal(80,observed.RecordedNet);else Assert.Null(observed.RecordedNet);
        using var check=fixture.Context();Assert.Equal(1,await check.SupplierPayouts.CountAsync(x=>x.OrderId==s.Order));
    }
}
