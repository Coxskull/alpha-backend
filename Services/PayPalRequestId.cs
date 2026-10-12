using System;
using System.Security.Cryptography;
using System.Text;

namespace Alpha.API.Services;

/// <summary>Produces a stable, provider-safe idempotency header for a single PayPal order capture.</summary>
public static class PayPalRequestId
{
    public static string ForCapture(string paypalOrderId)
    {
        if (string.IsNullOrWhiteSpace(paypalOrderId))
            throw new ArgumentException("A PayPal order ID is required.", nameof(paypalOrderId));

        // 36 ASCII characters; SHA-256 prevents exposing raw order IDs in header values.
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes("capture:" + paypalOrderId));
        return "cap-" + Convert.ToHexString(digest).Substring(0, 32).ToLowerInvariant();
    }
}
