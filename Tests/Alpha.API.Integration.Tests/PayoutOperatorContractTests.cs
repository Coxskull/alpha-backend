using Alpha.API.Models.Entrepreneur;
using Alpha.API.Services;
using Alpha.API.Services.Entrepreneur;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Xunit;
namespace Alpha.API.Integration.Tests;
public sealed partial class RefundRecoveryTests
{
    [Fact]
    public async Task OperatorQuoteShowsZeroNetCarryForwardWithoutConsumingDebt()
    {
        var source = await Settlement(); var refund = await Refund(source.Payment, 60); await Allocate(refund, Plan(source.Queue, 60));
        var future = await Settlement(amount: 20, paid: false, payee: source.Payee);
        using var db = fixture.Context();
        var quote = JsonSerializer.SerializeToElement(await new PayoutAccountingService(db).SettlementAsync(future.Queue, null, Guid.Empty, null, Ct));
        Assert.Equal(20m, quote.GetProperty("grossAmount").GetDecimal());
        Assert.Equal(20m, quote.GetProperty("recoveryOffset").GetDecimal());
        Assert.Equal(0m, quote.GetProperty("netAmount").GetDecimal());
        Assert.Equal(40m, quote.GetProperty("remainingRecoveryBalance").GetDecimal());
        Assert.True(quote.GetProperty("zeroNet").GetBoolean());
        Assert.True(quote.GetProperty("quoteIsAdvisory").GetBoolean());
        Assert.True(quote.GetProperty("eligibleForConfirmation").GetBoolean());
        Assert.Equal(0m, (await db.Set<Alpha.API.Models.RefundAllocation>().SingleAsync(a => a.OperationId == refund && a.FutureRecovery)).RecoveredAmount);
        var paid = JsonSerializer.SerializeToElement(await new PayoutAccountingService(db).SettlementAsync(future.Queue,
            new() { PaidAmount = 0, Currency = "USD", PayoutReference = "synthetic-zero-offset" }, Admin, null, Ct));
        Assert.False(paid.GetProperty("confirmationRequired").GetBoolean());
        Assert.False(paid.GetProperty("eligibleForConfirmation").GetBoolean());
        Assert.Equal(40m, paid.GetProperty("remainingRecoveryBalance").GetDecimal());
    }

    [Fact]
    public async Task AnotherPayoutMakesOldNetConfirmationRefreshableWithoutPartialWrites()
    {
        var source = await Settlement(); var refund = await Refund(source.Payment, 30); await Allocate(refund, Plan(source.Queue, 30));
        var first = await Settlement(amount: 50, paid: false, payee: source.Payee);
        var second = await Settlement(amount: 50, paid: false, payee: source.Payee);
        using (var db = fixture.Context()) Assert.Equal(20m, Net(await new PayoutAccountingService(db).SettlementAsync(first.Queue, null, Guid.Empty, null, Ct)));
        await Pay(second.Queue, 20);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Pay(first.Queue, 20));
        Assert.Equal("payout_quote_changed", PayoutOperatorContract.Code(error));
        using (var db = fixture.Context()) Assert.False(await db.SupplierPayouts.AnyAsync(p => p.OrderId == first.Order));
        Assert.Equal(50m, await Pay(first.Queue, 50));
    }

    [Fact]
    public async Task EntrepreneurQuoteReportsThresholdBeforeConfirmationAndZeroNetRemainsEligible()
    {
        using var db = fixture.Context();
        var old = await db.EntrepreneurProgramConfigurations.ToListAsync();
        var values = old.ToDictionary(x => x.id, x => x.MinimumPayoutThreshold);
        EntrepreneurProgramConfiguration? added = null;
        try
        {
            if(old.Count == 0) { added = new() { MinimumPayoutThreshold = 50 }; db.Add(added); }
            else foreach(var row in old) row.MinimumPayoutThreshold = 50;
            await db.SaveChangesAsync();
            var earning = await Earning(false);
            var quote = JsonSerializer.SerializeToElement(await new EntrepreneurPayoutService(db).QuoteAsync(earning.Earning, Ct));
            Assert.Equal(40m, quote.GetProperty("netAmount").GetDecimal());
            Assert.False(quote.GetProperty("eligibleForConfirmation").GetBoolean());
            Assert.Equal("payout_threshold_not_met", quote.GetProperty("blockedReasonCode").GetString());
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new EntrepreneurPayoutService(db).MarkPaidAsync(earning.Earning, "synthetic-threshold", Admin, 40, "USD", Ct));
            Assert.Equal("payout_threshold_not_met", PayoutOperatorContract.Code(error));
            var source = await Earning(true, amount: 60); var refund = await Refund(source.Payment, 60); await Allocate(refund, Plan(source.Earning, 60, "earning"));
            var future = await Earning(false, source.Payee, amount: 40);
            var zero = JsonSerializer.SerializeToElement(await new EntrepreneurPayoutService(db).QuoteAsync(future.Earning, Ct));
            Assert.True(zero.GetProperty("zeroNet").GetBoolean()); Assert.True(zero.GetProperty("eligibleForConfirmation").GetBoolean());
            Assert.Equal(20m, zero.GetProperty("remainingRecoveryBalance").GetDecimal());
        }
        finally
        {
            if(added != null) await db.EntrepreneurProgramConfigurations.Where(x => x.id == added.id).ExecuteDeleteAsync();
            foreach(var pair in values) await db.EntrepreneurProgramConfigurations.Where(x => x.id == pair.Key).ExecuteUpdateAsync(s => s.SetProperty(x => x.MinimumPayoutThreshold, pair.Value));
        }
    }
}
