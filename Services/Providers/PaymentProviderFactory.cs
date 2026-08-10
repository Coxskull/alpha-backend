using System;
using System.Collections.Generic;
using System.Linq;

namespace Alpha.API.Services.Providers;

public class PaymentProviderFactory
{
    private readonly IEnumerable<IPaymentProvider> _providers;

    public PaymentProviderFactory(
        IEnumerable<IPaymentProvider> providers)
    {
        _providers = providers;
    }

    public IPaymentProvider Get(string gateway)
    {
        if (string.IsNullOrWhiteSpace(gateway))
        {
            throw new ArgumentException(
                "Payment gateway is required.",
                nameof(gateway));
        }

        return _providers.FirstOrDefault(
            x => x.Name.Equals(
                gateway,
                StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"Payment gateway '{gateway}' is not configured.");
    }
}