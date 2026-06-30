// Services/PayPalService.cs
using Alpha.API.Models;
using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Alpha.API.Services;

public class PayPalService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;

    public PayPalService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _config = config;
    }

    private string BaseUrl => _config["PayPal:BaseUrl"] ?? "https://api-m.sandbox.paypal.com";
    private string ClientId => _config["PayPal:ClientId"] ?? throw new Exception("Missing PayPal ClientId");
    private string ClientSecret => _config["PayPal:ClientSecret"] ?? throw new Exception("Missing PayPal ClientSecret");

    public async Task<string> GetAccessToken()
    {
        var basic = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{ClientId}:{ClientSecret}")
        );

        var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/v1/oauth2/token");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials"
        });

        var response = await _http.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"PayPal token error: {json}");

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("access_token").GetString()!;
    }

    public async Task<string> CreateOrder(Order order, OrderFinancial financial)
    {
        var token = await GetAccessToken();

        var payload = new
        {
            intent = "CAPTURE",
            purchase_units = new[]
            {
                new
                {
                    reference_id = order.Id.ToString(),
                    custom_id = order.Id.ToString(),
                    invoice_id = order.OrderNumber,
                    amount = new
                    {
                        currency_code = financial.Currency ?? "USD",
                        value = financial.TotalAmount.ToString("0.00")
                    }
                }
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/v2/checkout/orders");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json"
        );

        var response = await _http.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"PayPal create order error: {json}");

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("id").GetString()!;
    }

    public async Task<JsonDocument> CaptureOrder(string paypalOrderId)
    {
        var token = await GetAccessToken();

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{BaseUrl}/v2/checkout/orders/{paypalOrderId}/capture"
        );

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = await _http.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"PayPal capture error: {json}");

        return JsonDocument.Parse(json);
    }
}