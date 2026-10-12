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
}
