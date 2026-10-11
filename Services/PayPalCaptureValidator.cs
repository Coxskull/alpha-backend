using System;
using System.Globalization;
using System.Text.Json;

namespace Alpha.API.Services;

/// <summary>
/// Validates the PayPal capture payload against the Alpha order and its expected total.
/// Missing or inconsistent provider fields are rejected; an order ID is never treated as a capture ID.
/// </summary>
public static class PayPalCaptureValidator
{
    public static bool TryValidate(
        JsonElement root,
        string expectedPayPalOrderId,
        Guid alphaOrderId,
        decimal expectedAmount,
        string expectedCurrency,
        out string captureId)
    {
        captureId = string.Empty;

        if (root.ValueKind != JsonValueKind.Object ||
            !TryGetString(root, "id", out var returnedOrderId) ||
            !string.Equals(returnedOrderId, expectedPayPalOrderId, StringComparison.Ordinal) ||
            !TryGetString(root, "status", out var orderStatus) ||
            !string.Equals(orderStatus, "COMPLETED", StringComparison.Ordinal))
        {
            return false;
        }

        if (!root.TryGetProperty("purchase_units", out var units) ||
            units.ValueKind != JsonValueKind.Array ||
            units.GetArrayLength() != 1)
        {
            return false;
        }

        var unit = units[0];
        var expectedAlphaOrderId = alphaOrderId.ToString("D");

        if (!TryGetString(unit, "custom_id", out var customId) ||
            !string.Equals(customId, expectedAlphaOrderId, StringComparison.OrdinalIgnoreCase) ||
            !TryGetString(unit, "reference_id", out var referenceId) ||
            !string.Equals(referenceId, expectedAlphaOrderId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!unit.TryGetProperty("payments", out var payments) ||
            payments.ValueKind != JsonValueKind.Object ||
            !payments.TryGetProperty("captures", out var captures) ||
            captures.ValueKind != JsonValueKind.Array ||
            captures.GetArrayLength() != 1)
        {
            return false;
        }

        var capture = captures[0];
        if (!TryGetString(capture, "id", out var id) ||
            string.IsNullOrWhiteSpace(id) ||
            !TryGetString(capture, "status", out var captureStatus) ||
            !string.Equals(captureStatus, "COMPLETED", StringComparison.Ordinal))
        {
            return false;
        }

        if (!capture.TryGetProperty("amount", out var amount) ||
            amount.ValueKind != JsonValueKind.Object ||
            !TryGetString(amount, "value", out var amountText) ||
            !decimal.TryParse(amountText, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsedAmount) ||
            parsedAmount != expectedAmount ||
            !TryGetString(amount, "currency_code", out var currency) ||
            !string.Equals(currency, expectedCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        captureId = id;
        return true;
    }

    private static bool TryGetString(JsonElement parent, string propertyName, out string value)
    {
        value = string.Empty;
        if (parent.ValueKind != JsonValueKind.Object ||
            !parent.TryGetProperty(propertyName, out var element) ||
            element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = element.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }
}
