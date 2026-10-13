using System.Text.Json;
namespace Alpha.API.Services;

public static class PayMongoCheckoutValidator
{
    public static bool TryValidate(JsonElement root, string sessionId, Guid orderId, decimal amount, string currency, out string paymentId)
    {
        paymentId = string.Empty;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("data", out var data)
            || data.ValueKind != JsonValueKind.Object || !Text(data, "id", out var returnedSession) || returnedSession != sessionId
            || !data.TryGetProperty("attributes", out var attributes) || attributes.ValueKind != JsonValueKind.Object
            || !Text(attributes, "payment_status", out var status) || status != "paid"
            || !attributes.TryGetProperty("metadata", out var metadata) || metadata.ValueKind != JsonValueKind.Object
            || !Text(metadata, "order_id", out var reference) || !Guid.TryParse(reference, out var returnedOrder) || returnedOrder != orderId
            || !attributes.TryGetProperty("payments", out var payments) || payments.ValueKind != JsonValueKind.Array
            || payments.GetArrayLength() != 1) return false;
        var payment = payments[0];
        if (payment.ValueKind != JsonValueKind.Object || !Text(payment, "id", out var id) || string.IsNullOrWhiteSpace(id)
            || !payment.TryGetProperty("attributes", out var details) || details.ValueKind != JsonValueKind.Object
            || !Text(details, "status", out var paymentStatus) || paymentStatus != "paid"
            || !Text(details, "currency", out var code) || code != currency
            || !details.TryGetProperty("amount", out var value) || value.ValueKind != JsonValueKind.Number
            || !value.TryGetDecimal(out var returnedAmount) || returnedAmount <= 0 || returnedAmount != amount * 100m)
            return false;
        paymentId = id;
        return true;
    }
    private static bool Text(JsonElement root, string key, out string value)
    {
        value = string.Empty;
        if (!root.TryGetProperty(key, out var item) || item.ValueKind != JsonValueKind.String) return false;
        value = item.GetString() ?? string.Empty;
        return true;
    }
}
