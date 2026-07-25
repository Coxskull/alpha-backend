using System;
using System.Linq;

namespace Alpha.API.Services;

public class CountryCurrencyService
{
    public (string Country, string Currency) Resolve(
        string? requestedCountry,
        string? requestedCurrency)
    {
        var country = string.IsNullOrWhiteSpace(requestedCountry)
            ? "MX"
            : requestedCountry.Trim().ToUpperInvariant();

        var allowedCountries = new[]
        {
            "PH",
            "MX",
            "US"
        };

        if (!allowedCountries.Contains(country))
        {
            throw new ArgumentException(
                "Country must be PH, MX, or US.");
        }

        var requiredCurrency = country switch
        {
            "PH" => "PHP",
            "MX" => "MXN",
            "US" => "USD",
            _ => throw new InvalidOperationException()
        };

        var currency = string.IsNullOrWhiteSpace(requestedCurrency)
            ? requiredCurrency
            : requestedCurrency.Trim().ToUpperInvariant();

        if (currency != requiredCurrency)
        {
            throw new ArgumentException(
                $"Orders from {country} must use {requiredCurrency}.");
        }

        return (country, currency);
    }
}