using Alpha.API.Models;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
namespace Alpha.API.Services;

public class PayPalService(HttpClient http, IConfiguration configuration)
{
    private string BaseUrl => configuration["PayPal:BaseUrl"] ?? "https://api-m.sandbox.paypal.com";

    public async Task<string> GetAccessToken(CancellationToken cancellationToken = default)
    {
        var clientId = configuration["PayPal:ClientId"] ?? throw new InvalidOperationException("PayPal credentials are missing.");
        var secret = configuration["PayPal:ClientSecret"] ?? throw new InvalidOperationException("PayPal credentials are missing.");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/v1/oauth2/token");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{secret}")));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials" });
        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException("PayPal authentication failed.");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return document.RootElement.GetProperty("access_token").GetString() ?? throw new InvalidOperationException("PayPal token is missing.");
    }

    private async Task<JsonDocument> SendAsync(HttpMethod method, string path, object? body,
        string? requestId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, $"{BaseUrl}/{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessToken(cancellationToken));
        request.Headers.TryAddWithoutValidation("Prefer", "return=representation");
        if (requestId != null) request.Headers.Add("PayPal-Request-Id", requestId);
        if (body != null) request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException("PayPal operation could not be confirmed.");
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    public async Task<string> CreateOrder(Order order, OrderFinancial financial, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Post, "v2/checkout/orders", new
        {
            intent = "CAPTURE", purchase_units = new[] { new { reference_id = order.Id.ToString(), custom_id = order.Id.ToString(),
                invoice_id = order.OrderNumber, amount = new { currency_code = financial.Currency,
                    value = financial.TotalAmount.ToString("0.00", CultureInfo.InvariantCulture) } } }
        }, order.Id.ToString("N"), cancellationToken);
        return response.RootElement.GetProperty("id").GetString() ?? throw new InvalidOperationException("PayPal order ID is missing.");
    }
    public Task<JsonDocument> CaptureOrder(string id, string requestId, CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Post, $"v2/checkout/orders/{Uri.EscapeDataString(id)}/capture", new { }, requestId, cancellationToken);
    public Task<JsonDocument> RefundCapture(string id, decimal amount, string currency, string requestId, CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Post, $"v2/payments/captures/{Uri.EscapeDataString(id)}/refund", new
        { amount = new { value = amount.ToString("0.00", CultureInfo.InvariantCulture), currency_code = currency } }, requestId, cancellationToken);
    public Task<JsonDocument> GetOrder(string id, CancellationToken cancellationToken)
        => SendAsync(HttpMethod.Get, $"v2/checkout/orders/{Uri.EscapeDataString(id)}", null, null, cancellationToken);
    public Task<JsonDocument> GetRefund(string id, CancellationToken cancellationToken)
        => SendAsync(HttpMethod.Get, $"v2/payments/refunds/{Uri.EscapeDataString(id)}", null, null, cancellationToken);
}
