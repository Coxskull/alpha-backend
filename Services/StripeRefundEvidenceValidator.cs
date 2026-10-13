using Stripe;
namespace Alpha.API.Services;

public static class StripeRefundEvidenceValidator
{
    // These are the application's supported order currencies; all use two minor-unit digits.
    public static bool SupportedCurrency(string currency) => currency is "USD" or "PHP" or "MXN";

    public static bool TryGetCompleted(Charge charge, string currency, out IReadOnlyList<Refund> refunds)
    {
        refunds = Array.Empty<Refund>();
        var list = charge.Refunds;
        if (!SupportedCurrency(currency) || string.IsNullOrWhiteSpace(charge.Id) || charge.Id.Length > 128
            || string.IsNullOrWhiteSpace(charge.PaymentIntentId) || charge.PaymentIntentId.Length > 128
            || list == null || list.Object != "list" || list.HasMore || list.Data == null || list.Data.Count == 0
            || charge.AmountRefunded <= 0 || charge.AmountRefunded > charge.Amount) return false;
        if (list.Data.Any(r => r == null || string.IsNullOrWhiteSpace(r.Id) || r.Id.Length > 110
            || r.Status != "succeeded" || r.Amount <= 0 || r.Amount > charge.AmountRefunded
            || !string.Equals(r.Currency, currency, StringComparison.OrdinalIgnoreCase)
            || r.ChargeId != charge.Id
            || (!string.IsNullOrEmpty(r.PaymentIntentId) && r.PaymentIntentId != charge.PaymentIntentId))
            || list.Data.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count() != list.Data.Count) return false;
        // Decimal avoids overflow from untrusted provider-sized integers during summation.
        if (list.Data.Sum(r => (decimal)r.Amount) != charge.AmountRefunded) return false;
        refunds = list.Data.OrderBy(r => r.Id, StringComparer.Ordinal).ToArray();
        return true;
    }
}
