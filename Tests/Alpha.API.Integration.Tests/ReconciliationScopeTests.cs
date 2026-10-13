using Alpha.API.Models;
using Alpha.API.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Xunit;
namespace Alpha.API.Integration.Tests;
public sealed partial class RefundRecoveryTests
{
    [Fact]
    public async Task OversizedReconciliationSourceCannotHideAnotherQueuePage()
    {
        var actor=await ReconciliationAdmin();var first=await fixture.SeedAsync();var second=await fixture.SeedAsync();
        var firstId=Guid.Parse("00000000-0000-0000-0000-000000000001");var secondId=Guid.Parse("00000000-0000-0000-0000-000000000002");
        using var db=fixture.Context();var payee=Guid.NewGuid();db.Suppliers.Add(new Supplier {Id=payee,Name="Synthetic scope supplier"});
        var financial=await db.OrderFinancials.Where(x=>x.OrderId==first.OrderId).Select(x=>x.Id).SingleAsync();
        var nextFinancial=await db.OrderFinancials.Where(x=>x.OrderId==second.OrderId).Select(x=>x.Id).SingleAsync();
        db.SettlementQueue.AddRange(new SettlementQueue {Id=firstId,OrderFinancialId=financial,PayeeType="supplier",PayeeId=payee,Amount=50,Status="ready_for_payout"},new SettlementQueue {Id=secondId,OrderFinancialId=nextFinancial,PayeeType="supplier",PayeeId=payee,Amount=30,Status="ready_for_payout"});
        for(var i=0;i<201;i++)db.SupplierPayouts.Add(new SupplierPayout {Id=Guid.NewGuid(),SupplierId=payee,OrderId=first.OrderId,Amount=1,Currency="USD",PayoutStatus="paid",PaidAt=DateTime.UtcNow});
        await db.SaveChangesAsync();var service=new PayoutReconciliationService(db);
        var one=JsonSerializer.SerializeToElement(await service.QueueAsync("settlement",null,1,actor,Ct));
        var row=one.GetProperty("rows")[0];Assert.Equal(firstId,row.GetProperty("Id").GetGuid());Assert.False(row.GetProperty("InspectionAvailable").GetBoolean());
        Assert.Equal(JsonValueKind.Null,row.GetProperty("ExpectedNet").ValueKind);Assert.Contains("manual_review_scope_exceeded",row.GetProperty("Issues").ToString());
        var two=JsonSerializer.SerializeToElement(await service.QueueAsync("settlement",one.GetProperty("next").GetGuid(),1,actor,Ct));
        Assert.Equal(secondId,two.GetProperty("rows")[0].GetProperty("Id").GetGuid());Assert.True(two.GetProperty("rows")[0].GetProperty("InspectionAvailable").GetBoolean());
        var fingerprint=row.GetProperty("Fingerprint").GetString()!;
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.InspectAsync("settlement",firstId,actor,Ct));
        var captureId=Guid.NewGuid();await Assert.ThrowsAsync<InvalidOperationException>(()=>service.CaptureAsync("settlement",firstId,new(captureId,fingerprint),actor,Ct));
        Assert.False(await db.AuditLogs.AnyAsync(x=>x.Id==captureId));
    }
    [Fact]
    public async Task LegacyCaptureRemainsImmutableAndReplayableAfterSourceChanges()
    {
        var source=await Settlement(paid:false);var actor=await ReconciliationAdmin();using var db=fixture.Context();var service=new PayoutReconciliationService(db);
        var snapshot=await service.InspectAsync("settlement",source.Queue,actor,Ct);var capture=new ReconciliationCapture(Guid.NewGuid(),actor,DateTime.UtcNow,snapshot);
        var legacy=System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(capture))!;legacy["Snapshot"]!.AsObject().Remove("InspectionAvailable");
        var payload="Payout reconciliation v1;"+legacy.ToJsonString();Assert.DoesNotContain("InspectionAvailable",payload);
        db.AuditLogs.Add(new AuditLog {Id=capture.Id,OrderId=source.Order,Action=payload,PerformedBy=actor.ToString(),CreatedAt=capture.CapturedAt});await db.SaveChangesAsync();
        await Pay(source.Queue,80);
        var replay=await service.CaptureAsync("settlement",source.Queue,new(capture.Id,snapshot.Fingerprint),actor,Ct);
        Assert.Equal(snapshot.Fingerprint,replay.Snapshot.Fingerprint);Assert.True(replay.Snapshot.InspectionAvailable);Assert.Null(replay.Snapshot.RecordedNet);
        Assert.Equal(payload,(await db.AuditLogs.AsNoTracking().SingleAsync(x=>x.Id==capture.Id)).Action);
        var history=JsonSerializer.SerializeToElement(await service.HistoryAsync("settlement",source.Queue,null,1,actor,Ct));Assert.Equal(capture.Id,history.GetProperty("rows")[0].GetProperty("Id").GetGuid());
        var other=await Settlement(paid:false);Assert.Empty(JsonSerializer.SerializeToElement(await service.HistoryAsync("settlement",other.Queue,null,1,actor,Ct)).GetProperty("rows").EnumerateArray());
    }
}
