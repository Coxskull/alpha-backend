using System.Text.Json;
using Alpha.API.Services;
using Xunit;

namespace Alpha.API.Tests.Services;

public sealed class PayPalRefundValidatorTests
{
    [Fact]
    public void AcceptsCompletedRefundWithProviderId()
    {
        using var response = JsonDocument.Parse("""{"id":"REFUND-123","status":"COMPLETED"}""");
        Assert.True(PayPalRefundValidator.TryGetCompletedRefundId(response.RootElement, out var id));
        Assert.Equal("REFUND-123", id);
    }

    [Theory]
    [InlineData("""{"id":"REFUND-123","status":"PENDING"}""")]
    [InlineData("""{"id":"REFUND-123","status":"FAILED"}""")]
    [InlineData("""{"id":"REFUND-123","status":"CANCELLED"}""")]
    [InlineData("""{"id":"REFUND-123","status":"COMPLETED_PENDING"}""")]
    [InlineData("""{"id":"REFUND-123"}""")]
    [InlineData("""{"status":"COMPLETED"}""")]
    [InlineData("""{"id":"","status":"COMPLETED"}""")]
    [InlineData("""{"id":null,"status":"COMPLETED"}""")]
    [InlineData("""{"id":42,"status":"COMPLETED"}""")]
    [InlineData("""{"id":"REFUND-123","status":null}""")]
    [InlineData("[]")]
    public void RejectsIncompleteOrNonCompletedRefund(string json)
    {
        using var response = JsonDocument.Parse(json);
        Assert.False(PayPalRefundValidator.TryGetCompletedRefundId(response.RootElement, out var id));
        Assert.Equal(string.Empty, id);
    }
}
