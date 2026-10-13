using Alpha.API.Models;
using Alpha.API.Models.Entrepreneur;
using Alpha.API.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Xunit;
namespace Alpha.API.Integration.Tests;
public sealed partial class RefundRecoveryTests
{
    private static Guid Before(Guid id)
    {
        var value=System.Numerics.BigInteger.Parse("0"+id.ToString("N"),System.Globalization.NumberStyles.HexNumber)-1;
        var hex=value.ToString("x").PadLeft(32,'0');return Guid.ParseExact(hex[^32..],"N");
    }
    [Theory] [InlineData("supplier")] [InlineData("driver")]
    public async Task RecordInventoryDetectsMissingSourceAndDoesNotWriteAnything(string kind)
    {
        var source=await Settlement(kind);var actor=await ReconciliationAdmin();using var db=fixture.Context();
        if(kind=="supplier")await db.SupplierPayouts.Where(x=>x.OrderId==source.Order).ExecuteUpdateAsync(x=>x.SetProperty(p=>p.Amount,80.01m));
        else await db.DriverPayouts.Where(x=>x.OrderId==source.Order).ExecuteUpdateAsync(x=>x.SetProperty(p=>p.Amount,80.01m));
        await db.SettlementQueue.Where(x=>x.Id==source.Queue).ExecuteDeleteAsync();var audits=await db.AuditLogs.CountAsync();
        var record=kind=="supplier"?await db.SupplierPayouts.Where(x=>x.OrderId==source.Order).Select(x=>x.Id).SingleAsync():await db.DriverPayouts.Where(x=>x.OrderId==source.Order).Select(x=>x.Id).SingleAsync();
        var result=JsonSerializer.SerializeToElement(await new PayoutRecordInventoryService(db).ReadAsync(kind,Before(record),1,actor,Ct));
        Assert.Contains(result.GetProperty("rows").EnumerateArray(),r=>r.GetProperty("OrderId").GetGuid()==source.Order&&r.GetProperty("issues").ToString().Contains("missing_payout_source"));
        Assert.Equal(JsonValueKind.String,result.GetProperty("rows")[0].GetProperty("amount").ValueKind);Assert.Equal("80.01",result.GetProperty("rows")[0].GetProperty("amount").GetString());
        Assert.Equal(audits,await db.AuditLogs.CountAsync());Assert.False(await db.SettlementQueue.AnyAsync(x=>x.Id==source.Queue));
    }
    [Fact]
    public async Task RecordInventoryDoesNotMatchAnotherPayeeRoleOrCurrency()
    {
        var source=await Settlement("supplier");var actor=await ReconciliationAdmin();using var db=fixture.Context();
        await db.SupplierPayouts.Where(x=>x.OrderId==source.Order).ExecuteUpdateAsync(x=>x.SetProperty(p=>p.Currency,"EUR"));
        var record=await db.SupplierPayouts.Where(x=>x.OrderId==source.Order).Select(x=>x.Id).SingleAsync();
        var result=JsonSerializer.SerializeToElement(await new PayoutRecordInventoryService(db).ReadAsync("supplier",Before(record),1,actor,Ct));
        Assert.Contains(result.GetProperty("rows").EnumerateArray(),r=>r.GetProperty("OrderId").GetGuid()==source.Order&&r.GetProperty("issues").ToString().Contains("source_state_or_currency_mismatch"));
        var other=Guid.NewGuid();db.Suppliers.Add(new Supplier {Id=other,Name="Synthetic other inventory payee"});await db.SaveChangesAsync();
        await db.SettlementQueue.Where(x=>x.Id==source.Queue).ExecuteUpdateAsync(x=>x.SetProperty(q=>q.PayeeId,other));
        result=JsonSerializer.SerializeToElement(await new PayoutRecordInventoryService(db).ReadAsync("supplier",Before(record),1,actor,Ct));
        Assert.Equal(0,result.GetProperty("rows")[0].GetProperty("SourceCount").GetInt64());
        await db.SettlementQueue.Where(x=>x.Id==source.Queue).ExecuteUpdateAsync(x=>x.SetProperty(q=>q.PayeeId,source.Payee).SetProperty(q=>q.PayeeType,"driver"));
        result=JsonSerializer.SerializeToElement(await new PayoutRecordInventoryService(db).ReadAsync("supplier",Before(record),1,actor,Ct));
        Assert.Contains(result.GetProperty("rows").EnumerateArray(),r=>r.GetProperty("OrderId").GetGuid()==source.Order&&r.GetProperty("SourceCount").GetInt64()==0);
    }
    [Fact]
    public async Task SourceLessBatchAndCursorRemainExplicitAndStable()
    {
        var actor=await ReconciliationAdmin();using var db=fixture.Context();var first=Guid.Parse("00000000-0000-0000-0000-000000000011");var second=Guid.Parse("00000000-0000-0000-0000-000000000012");
        db.EntrepreneurPayoutBatches.AddRange(new EntrepreneurPayoutBatch {Id=first,Currency="USD",TotalAmount=20,EarningCount=1,Status="PAID",PayoutReference="synthetic-orphan"},new EntrepreneurPayoutBatch {Id=second,Currency="EUR",TotalAmount=30,EarningCount=1,Status="PAID"});await db.SaveChangesAsync();
        var service=new PayoutRecordInventoryService(db);var one=JsonSerializer.SerializeToElement(await service.ReadAsync("batch",null,1,actor,Ct));Assert.Equal(first,one.GetProperty("rows")[0].GetProperty("Id").GetGuid());Assert.Equal(JsonValueKind.Null,one.GetProperty("rows")[0].GetProperty("PayeeId").ValueKind);
        var two=JsonSerializer.SerializeToElement(await service.ReadAsync("batch",one.GetProperty("next").GetGuid(),1,actor,Ct));Assert.Equal(second,two.GetProperty("rows")[0].GetProperty("Id").GetGuid());
    }
    [Theory] [InlineData("dispatcher")] [InlineData("supplier")]
    public async Task RecordInventoryRequiresCurrentAdministrator(string role)
    {var actor=await ReconciliationAdmin(role);using var db=fixture.Context();await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>new PayoutRecordInventoryService(db).ReadAsync("supplier",null,20,actor,Ct));}
}
