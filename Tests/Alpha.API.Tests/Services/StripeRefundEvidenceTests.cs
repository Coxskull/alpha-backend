using Alpha.API.Services;
using Stripe;
using Xunit;
namespace Alpha.API.Tests.Services;

public sealed class StripeRefundEvidenceTests
{
    private static Charge Evidence(string currency = "usd") => new()
    {
        Id = "ch_synthetic", PaymentIntentId = "pi_synthetic", Currency = currency, Amount = 10000, AmountRefunded = 3000,
        Refunds = new StripeList<Refund> { Object = "list", HasMore = false, Data = new List<Refund> {
            new() { Id = "re_synthetic", ChargeId = "ch_synthetic", PaymentIntentId = "pi_synthetic", Amount = 3000, Currency = currency, Status = "succeeded" } } }
    };
    [Theory]
    [InlineData("USD")]
    [InlineData("MXN")]
    [InlineData("PHP")]
    public void CompleteSuccessfulRefundEvidenceMatchesSupportedCurrency(string currency)
    {
        Assert.True(StripeRefundEvidenceValidator.TryGetCompleted(Evidence(currency.ToLowerInvariant()), currency, out var refunds));
        Assert.Single(refunds); Assert.Equal(30m, refunds[0].Amount / 100m);
    }
    [Theory]
    [InlineData("pending")]
    [InlineData("failed")]
    [InlineData("canceled")]
    [InlineData("requires_action")]
    public void UnconfirmedRefundsAreNotAllocatable(string status)
    {
        var charge = Evidence(); charge.Refunds.Data[0].Status = status;
        Assert.False(StripeRefundEvidenceValidator.TryGetCompleted(charge, "USD", out _));
    }
    [Theory]
    [InlineData("JPY")]
    [InlineData("BHD")]
    [InlineData("EUR")]
    public void UnsupportedOrderCurrenciesCannotUseAnAssumedMinorUnitScale(string currency)
        => Assert.False(StripeRefundEvidenceValidator.TryGetCompleted(Evidence(currency), currency, out _));
    [Fact]
    public void TruncatedEmptyAndMissingListsAreNotCompleteEvidence()
    {
        var charge = Evidence(); charge.Refunds.HasMore = true;
        Assert.False(StripeRefundEvidenceValidator.TryGetCompleted(charge, "USD", out _));
        charge.Refunds.HasMore = false; charge.Refunds.Data.Clear();
        Assert.False(StripeRefundEvidenceValidator.TryGetCompleted(charge, "USD", out _));
        charge.Refunds = null!; Assert.False(StripeRefundEvidenceValidator.TryGetCompleted(charge, "USD", out _));
    }
    [Fact]
    public void DuplicateRefundIdsDoNotEstablishTwoCompletedOperations()
    {
        var charge = Evidence(); charge.AmountRefunded = 6000; charge.Refunds.Data.Add(charge.Refunds.Data[0]);
        Assert.False(StripeRefundEvidenceValidator.TryGetCompleted(charge, "USD", out _));
    }
    [Theory]
    [InlineData("currency")]
    [InlineData("charge")]
    [InlineData("intent")]
    [InlineData("sum")]
    [InlineData("zero")]
    public void MismatchedParentMoneyAndCurrencyEvidenceIsRejected(string field)
    {
        var charge = Evidence(); var refund = charge.Refunds.Data[0];
        if (field == "currency") refund.Currency = "php";
        if (field == "charge") refund.ChargeId = "ch_other";
        if (field == "intent") refund.PaymentIntentId = "pi_other";
        if (field == "sum") refund.Amount = 2999;
        if (field == "zero") refund.Amount = 0;
        Assert.False(StripeRefundEvidenceValidator.TryGetCompleted(charge, "USD", out _));
    }
}
