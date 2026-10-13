using Alpha.API.Models.Entrepreneur;
using Alpha.API.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Xunit;
namespace Alpha.API.Integration.Tests;
public sealed partial class RefundRecoveryTests
{
    [Theory]
    [InlineData("supplier",true)] [InlineData("driver",true)] [InlineData("batch",true)] [InlineData("supplier",false)]
    public async Task InvalidNumericInventoryRemainsVisibleAndDoesNotHideCursor(string kind,bool orphan)
    {
        var actor=await ReconciliationAdmin();Guid record;using var db=fixture.Context();
        if(kind=="batch")
        {
            record=Guid.NewGuid();db.EntrepreneurPayoutBatches.Add(new EntrepreneurPayoutBatch {Id=record,Currency="USD",TotalAmount=20,EarningCount=1,Status="PAID"});await db.SaveChangesAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE entrepreneur_payout_batches SET total_amount='NaN'::numeric WHERE id={record}");
        }
        else
        {
            var source=await Settlement(kind);record=kind=="supplier"?await db.SupplierPayouts.Where(x=>x.OrderId==source.Order).Select(x=>x.Id).SingleAsync():await db.DriverPayouts.Where(x=>x.OrderId==source.Order).Select(x=>x.Id).SingleAsync();
            if(orphan)await db.SettlementQueue.Where(x=>x.Id==source.Queue).ExecuteDeleteAsync();
            if(kind=="supplier")await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE supplier_payouts SET amount='NaN'::numeric WHERE id={record}");
            else await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE driver_payouts SET amount='NaN'::numeric WHERE id={record}");
        }
        var before=await db.AuditLogs.CountAsync();var result=JsonSerializer.SerializeToElement(await new PayoutRecordInventoryService(db).ReadAsync(kind,Before(record),1,actor,Ct));
        var row=result.GetProperty("rows")[0];Assert.Equal(record,row.GetProperty("Id").GetGuid());Assert.False(row.GetProperty("amountValid").GetBoolean());Assert.Equal(JsonValueKind.Null,row.GetProperty("amount").ValueKind);Assert.Contains("invalid_payout_amount",row.GetProperty("issues").ToString());
        Assert.Equal(before,await db.AuditLogs.CountAsync());
        if(kind=="supplier")await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE supplier_payouts SET amount=80 WHERE id={record}");
        else if(kind=="driver")await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE driver_payouts SET amount=80 WHERE id={record}");
        else await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE entrepreneur_payout_batches SET total_amount=20 WHERE id={record}");
    }
}
