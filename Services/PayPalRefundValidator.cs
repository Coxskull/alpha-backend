using System;
using System.Text.Json;

namespace Alpha.API.Services;

/// <summary>
/// Validates a completed PayPal refund response before any local ledger balance is advanced.
/// PENDING, FAILED and malformed responses must not be treated as completed refunds.
/// This validator alone does not provide durable idempotency or concurrency protection.
/// </summary>
public static class PayPalRefundValidator
{
    public static bool TryGetCompletedRefundId(JsonElement root, out string refundId)
    {
        refundId = string.Empty;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("id", out var id) ||
            id.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(id.GetString()) ||
            !root.TryGetProperty("status", out var status) ||
            status.ValueKind != JsonValueKind.String ||
            !string.Equals(status.GetString(), "COMPLETED", StringComparison.Ordinal))
        {
            return false;
        }

        refundId = id.GetString()!;
        return true;
    }

    public static bool TryValidate(JsonElement root, decimal amount, string currency, string captureId, out string refundId)
    {
        refundId = string.Empty;
        if (!TryGetCompletedRefundId(root, out var id)
            || !root.TryGetProperty("amount", out var money) || money.ValueKind != JsonValueKind.Object
            || !money.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.String
            || !decimal.TryParse(value.GetString(), System.Globalization.NumberStyles.AllowDecimalPoint,
                System.Globalization.CultureInfo.InvariantCulture, out var returnedAmount) || returnedAmount != amount
            || !money.TryGetProperty("currency_code", out var code) || code.ValueKind != JsonValueKind.String
            || code.GetString() != currency
            || !root.TryGetProperty("links", out var links) || links.ValueKind != JsonValueKind.Array)
            return false;
        var expectedPath = "/v2/payments/captures/" + Uri.EscapeDataString(captureId);
        var linkedCapture = links.EnumerateArray().Any(link => link.ValueKind == JsonValueKind.Object
            && link.TryGetProperty("rel", out var rel) && rel.ValueKind == JsonValueKind.String && rel.GetString() == "up"
            && link.TryGetProperty("href", out var href) && href.ValueKind == JsonValueKind.String
            && Uri.TryCreate(href.GetString(), UriKind.Absolute, out var uri) && uri.AbsolutePath == expectedPath);
        if (!linkedCapture) return false;
        refundId = id;
        return true;
    }
}
