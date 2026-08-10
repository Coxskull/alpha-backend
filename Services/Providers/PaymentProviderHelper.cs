using System;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Alpha.API.Services.Providers;

public static class PaymentProviderHelper
{
    public static string BasicAuth(
        string username,
        string password = "")
    {
        var value =
            Convert.ToBase64String(
                Encoding.UTF8.GetBytes(
                    $"{username}:{password}"));

        return $"Basic {value}";
    }

    public static async Task<JsonDocument> ReadJsonAsync(
        HttpResponseMessage response)
    {
        var content =
            await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Payment gateway returned " +
                $"{(int)response.StatusCode}: {content}");
        }

        return JsonDocument.Parse(content);
    }

    public static string? GetString(
        JsonElement element,
        params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (element.TryGetProperty(
                    propertyName,
                    out var value))
            {
                if (value.ValueKind ==
                    JsonValueKind.String)
                {
                    return value.GetString();
                }
            }
        }

        return null;
    }

    public static long ToMinorUnits(
        decimal amount)
    {
        return checked(
            (long)Math.Round(
                amount * 100m,
                MidpointRounding.AwayFromZero));
    }
}