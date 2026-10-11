using System.Text.Json;
using Alpha.API.Services;
using Xunit;

namespace Alpha.API.Tests.Services;

public sealed class PayPalCaptureValidatorTests
{
    private static readonly Guid AlphaOrderId = Guid.Parse("d1d93e77-82a6-4ec2-9df2-79a3b06c6b21");
    private const string PayPalOrderId = "PAYPAL-ORDER-123";

    private static JsonDocument Response(
        string orderStatus = "COMPLETED",
        string captureStatus = "COMPLETED",
        string orderId = PayPalOrderId,
        string customId = "d1d93e77-82a6-4ec2-9df2-79a3b06c6b21",
        string referenceId = "d1d93e77-82a6-4ec2-9df2-79a3b06c6b21",
        string captureId = "CAPTURE-456",
        string amount = "125.50",
        string currency = "USD")
    {
        var json = $$"""
        {
          "id": "{{orderId}}",
          "status": "{{orderStatus}}",
          "purchase_units": [{
            "custom_id": "{{customId}}",
            "reference_id": "{{referenceId}}",
            "payments": {
              "captures": [{
                "id": "{{captureId}}",
                "status": "{{captureStatus}}",
                "amount": { "value": "{{amount}}", "currency_code": "{{currency}}" }
              }]
            }
          }]
        }
        """;
        return JsonDocument.Parse(json);
    }

    private static bool Validate(JsonDocument response, out string captureId) =>
        PayPalCaptureValidator.TryValidate(
            response.RootElement, PayPalOrderId, AlphaOrderId, 125.50m, "USD", out captureId);

    [Fact]
    public void AcceptsMatchingCompletedCaptureAndReturnsCaptureId()
    {
        using var response = Response();
        Assert.True(Validate(response, out var captureId));
        Assert.Equal("CAPTURE-456", captureId);
    }

    [Theory]
    [InlineData("PENDING", "COMPLETED", "PAYPAL-ORDER-123", "d1d93e77-82a6-4ec2-9df2-79a3b06c6b21", "d1d93e77-82a6-4ec2-9df2-79a3b06c6b21", "CAPTURE-456", "125.50", "USD")]
    [InlineData("COMPLETED", "PENDING", "PAYPAL-ORDER-123", "d1d93e77-82a6-4ec2-9df2-79a3b06c6b21", "d1d93e77-82a6-4ec2-9df2-79a3b06c6b21", "CAPTURE-456", "125.50", "USD")]
    [InlineData("COMPLETED", "COMPLETED", "OTHER-ORDER", "d1d93e77-82a6-4ec2-9df2-79a3b06c6b21", "d1d93e77-82a6-4ec2-9df2-79a3b06c6b21", "CAPTURE-456", "125.50", "USD")]
    [InlineData("COMPLETED", "COMPLETED", "PAYPAL-ORDER-123", "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "d1d93e77-82a6-4ec2-9df2-79a3b06c6b21", "CAPTURE-456", "125.50", "USD")]
    [InlineData("COMPLETED", "COMPLETED", "PAYPAL-ORDER-123", "d1d93e77-82a6-4ec2-9df2-79a3b06c6b21", "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "CAPTURE-456", "125.50", "USD")]
    [InlineData("COMPLETED", "COMPLETED", "PAYPAL-ORDER-123", "d1d93e77-82a6-4ec2-9df2-79a3b06c6b21", "d1d93e77-82a6-4ec2-9df2-79a3b06c6b21", "", "125.50", "USD")]
    [InlineData("COMPLETED", "COMPLETED", "PAYPAL-ORDER-123", "d1d93e77-82a6-4ec2-9df2-79a3b06c6b21", "d1d93e77-82a6-4ec2-9df2-79a3b06c6b21", "CAPTURE-456", "125.51", "USD")]
    [InlineData("COMPLETED", "COMPLETED", "PAYPAL-ORDER-123", "d1d93e77-82a6-4ec2-9df2-79a3b06c6b21", "d1d93e77-82a6-4ec2-9df2-79a3b06c6b21", "CAPTURE-456", "125.50", "EUR")]
    public void RejectsNonCompletedOrMismatchedCapture(
        string orderStatus, string captureStatus, string orderId, string customId,
        string referenceId, string captureIdValue, string amount, string currency)
    {
        using var response = Response(orderStatus, captureStatus, orderId, customId, referenceId, captureIdValue, amount, currency);
        Assert.False(Validate(response, out var captureId));
        Assert.Equal(string.Empty, captureId);
    }

    [Fact]
    public void RejectsMissingCaptureDetailsWithoutUsingOrderIdAsFallback()
    {
        using var response = JsonDocument.Parse("""{"id":"PAYPAL-ORDER-123","status":"COMPLETED","purchase_units":[]}""");
        Assert.False(Validate(response, out var captureId));
        Assert.Equal(string.Empty, captureId);
    }
}
