using System.Text.Json;
using Alpha.API.Services;
using Xunit;
namespace Alpha.API.Tests.Services;

public sealed class PayMongoCheckoutValidatorTests
{
    [Theory]
    [InlineData("checkout-1", "paid", "10000", "PHP", true)]
    [InlineData("other-checkout", "paid", "10000", "PHP", false)]
    [InlineData("checkout-1", "pending", "10000", "PHP", false)]
    [InlineData("checkout-1", "paid", "9999", "PHP", false)]
    [InlineData("checkout-1", "paid", "10000", "USD", false)]
    public void RequiresMatchingCheckoutCompletedPaymentAmountAndCurrency(string checkout, string status, string amount, string currency, bool expected)
    {
        var order = Guid.NewGuid();
        using var response = JsonDocument.Parse(JsonSerializer.Serialize(new { data = new { id = checkout, attributes = new {
            payment_status = status, metadata = new { order_id = order.ToString() },
            payments = new[] { new { id = "payment-1", attributes = new { status = "paid", amount = decimal.Parse(amount), currency } } }
        } } }));
        Assert.Equal(expected, PayMongoCheckoutValidator.TryValidate(response.RootElement, "checkout-1", order, 100m, "PHP", out _));
        Assert.False(PayMongoCheckoutValidator.TryValidate(response.RootElement, "checkout-1", Guid.NewGuid(), 100m, "PHP", out _));
    }
}
