using System.Text.Json;
using Alpha.API.Services;
using Xunit;
namespace Alpha.API.Tests.Services;

public sealed class PayPalRefundMatchingTests
{
    private static string Payload(string status, string amount, string currency, string capture, string id = "REFUND-1")
        => JsonSerializer.Serialize(new { id, status, amount = new { value = amount, currency_code = currency },
            links = new[] { new { rel = "up", href = "https://api-m.sandbox.paypal.com/v2/payments/captures/" + capture } } });
    [Theory]
    [InlineData("PENDING", "10.00", "USD", "CAPTURE-1", "REFUND-1")]
    [InlineData("FAILED", "10.00", "USD", "CAPTURE-1", "REFUND-1")]
    [InlineData("COMPLETED", "11.00", "USD", "CAPTURE-1", "REFUND-1")]
    [InlineData("COMPLETED", "10.00", "PHP", "CAPTURE-1", "REFUND-1")]
    [InlineData("COMPLETED", "10.00", "USD", "OTHER-CAPTURE", "REFUND-1")]
    [InlineData("COMPLETED", "10.00", "USD", "CAPTURE-1", "")]
    public void RejectsUnconfirmedOrMismatchedRefund(string status, string amount, string currency, string capture, string id)
    {
        using var response = JsonDocument.Parse(Payload(status, amount, currency, capture, id));
        Assert.False(PayPalRefundValidator.TryValidate(response.RootElement, 10m, "USD", "CAPTURE-1", out _));
    }
    [Fact]
    public void AcceptsOnlyMatchingCompletedRefund()
    {
        using var response = JsonDocument.Parse(Payload("COMPLETED", "10.00", "USD", "CAPTURE-1"));
        Assert.True(PayPalRefundValidator.TryValidate(response.RootElement, 10m, "USD", "CAPTURE-1", out var id));
        Assert.Equal("REFUND-1", id);
    }
}
