using Alpha.API.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Xunit;
namespace Alpha.API.Integration.Tests;
public sealed partial class RefundRecoveryTests
{
    [Theory]
    [InlineData("supplier")] [InlineData("driver")] [InlineData("source")]
    [InlineData("financial")] [InlineData("earning")] [InlineData("batch")]
    public async Task NonfiniteReconciliationShowsManualReviewAndCannotCapture(string target)
    {
        var actor=await ReconciliationAdmin();using var db=fixture.Context();
        Guid id,record;string kind,table,column;
        if(target is "earning" or "batch")
        {
            var source=await Earning(true);id=source.Earning;kind="earning";
            record=target=="earning"?id:(await db.EntrepreneurEarnings.AsNoTracking().SingleAsync(x=>x.Id==id)).PayoutBatchId!.Value;
            table=target=="earning"?"entrepreneur_earnings":"entrepreneur_payout_batches";
            column=target=="earning"?"entrepreneur_earnings_amount":"total_amount";
        }
        else
        {
            var source=await Settlement(target=="driver"?"driver":"supplier");id=source.Queue;kind="settlement";
            table=target switch {"source"=>"settlement_queue","financial"=>"order_financials","driver"=>"driver_payouts",_=>"supplier_payouts"};column="amount";
            record=target switch {"source"=>id,"financial"=>await db.OrderFinancials.Where(x=>x.OrderId==source.Order).Select(x=>x.Id).SingleAsync(),"driver"=>await db.DriverPayouts.Where(x=>x.OrderId==source.Order).Select(x=>x.Id).SingleAsync(),_=>await db.SupplierPayouts.Where(x=>x.OrderId==source.Order).Select(x=>x.Id).SingleAsync()};
            if(target=="financial")column="supplier_amount";
        }
        // Table and column are fixed test constants; record stays parameterized.
        var originalService=new PayoutReconciliationService(db);
        var originalSnapshot=await originalService.InspectAsync(kind,id,actor,Ct);
        var capture=await originalService.CaptureAsync(kind,id,new(Guid.NewGuid(),originalSnapshot.Fingerprint),actor,Ct);
        var auditPayload=(await db.AuditLogs.AsNoTracking().SingleAsync(x=>x.Id==capture.Id)).Action;
        var sql=$"UPDATE {table} SET {column}='NaN'::numeric WHERE id={{0}}";
        await db.Database.ExecuteSqlRawAsync(sql,record);var before=await db.AuditLogs.CountAsync();
        try
        {
            var service=new PayoutReconciliationService(db);
            var page=JsonSerializer.SerializeToElement(await service.QueueAsync(kind,Before(id),1,actor,Ct));
            var row=page.GetProperty("rows")[0];Assert.Equal(id,row.GetProperty("Id").GetGuid());
            Assert.False(row.GetProperty("InspectionAvailable").GetBoolean());Assert.Contains("manual_review_nonfinite_numeric",row.GetProperty("Issues").ToString());
            foreach(var field in new[]{"GrossAmount","Adjustment","RecoveryOffset","ExpectedNet","RecordedNet","Reference"})Assert.Equal(JsonValueKind.Null,row.GetProperty(field).ValueKind);
            await Assert.ThrowsAsync<InvalidOperationException>(()=>service.InspectAsync(kind,id,actor,Ct));
            await Assert.ThrowsAsync<InvalidOperationException>(()=>service.CaptureAsync(kind,id,new(Guid.NewGuid(),row.GetProperty("Fingerprint").GetString()!),actor,Ct));
            var replay=await service.CaptureAsync(kind,id,new(capture.Id,originalSnapshot.Fingerprint),actor,Ct);
            Assert.Equal(originalSnapshot.Fingerprint,replay.Snapshot.Fingerprint);
            Assert.Equal(auditPayload,(await db.AuditLogs.AsNoTracking().SingleAsync(x=>x.Id==capture.Id)).Action);
            Assert.Equal(before,await db.AuditLogs.CountAsync());
            var readSql=$"SELECT {column}::text AS \"Value\" FROM {table} WHERE id={{0}}";
            Assert.Equal("NaN",await db.Database.SqlQueryRaw<string>(readSql,record).SingleAsync());
        }
        finally {var restoreSql=$"UPDATE {table} SET {column}=40 WHERE id={{0}}";await db.Database.ExecuteSqlRawAsync(restoreSql,record);}
    }
    [Fact]
    public async Task NonfiniteSourceDoesNotHideTheNextHealthyPage()
    {
        var actor=await ReconciliationAdmin();var bad=await Settlement(paid:false);var good=await Settlement(paid:false);
        var first=Guid.Parse("00000000-0000-0000-0000-000000000003");var second=Guid.Parse("00000000-0000-0000-0000-000000000004");
        using var db=fixture.Context();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE settlement_queue SET id={first}, amount='NaN'::numeric WHERE id={bad.Queue}");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE settlement_queue SET id={second} WHERE id={good.Queue}");
        try
        {
            var service=new PayoutReconciliationService(db);
            var page=JsonSerializer.SerializeToElement(await service.QueueAsync("settlement",Before(first),1,actor,Ct));
            Assert.Equal(first,page.GetProperty("rows")[0].GetProperty("Id").GetGuid());
            Assert.False(page.GetProperty("rows")[0].GetProperty("InspectionAvailable").GetBoolean());
            Assert.Equal(first,page.GetProperty("next").GetGuid());
            var next=JsonSerializer.SerializeToElement(await service.QueueAsync("settlement",first,1,actor,Ct));
            Assert.Equal(second,next.GetProperty("rows")[0].GetProperty("Id").GetGuid());
            Assert.True(next.GetProperty("rows")[0].GetProperty("InspectionAvailable").GetBoolean());
        }
        finally {await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE settlement_queue SET amount=40 WHERE id={first}");}
    }

}
