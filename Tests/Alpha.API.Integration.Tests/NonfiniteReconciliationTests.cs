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
}
