using System;
using Alpha.API.Services;
using Xunit;

namespace Alpha.API.Tests.Services;

public sealed class PayPalRequestIdTests
{
    [Fact]
    public void CaptureRetriesReuseSameRequestId()
    {
        var first = PayPalRequestId.ForCapture("PAYPAL-ORDER-1");
        Assert.Equal(first, PayPalRequestId.ForCapture("PAYPAL-ORDER-1"));
        Assert.Equal(36, first.Length);
        Assert.StartsWith("cap-", first);
    }

    [Fact]
    public void DifferentOrdersProduceDifferentRequestIds() =>
        Assert.NotEqual(PayPalRequestId.ForCapture("ORDER-1"), PayPalRequestId.ForCapture("ORDER-2"));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void RejectsMissingOrderId(string? orderId) =>
        Assert.Throws<ArgumentException>(() => PayPalRequestId.ForCapture(orderId!));
}
