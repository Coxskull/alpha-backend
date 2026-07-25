using Alpha.API.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Alpha.API.Services;

public class PayMongoService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _configuration;

    public PayMongoService(
        HttpClient http,
        IConfiguration configuration)
    {
        _http = http;
        _configuration = configuration;
    }

    private string BaseUrl =>
        _configuration["PayMongo:BaseUrl"]
        ?? "https://api.paymongo.com/v1";

    private string SecretKey =>
        _configuration["PayMongo:SecretKey"]
        ?? throw new InvalidOperationException(
            "PayMongo secret key is missing.");

    private string FrontendBaseUrl =>
        (_configuration["Frontend:BaseUrl"]
         ?? "http://localhost:3000").TrimEnd('/');

    private AuthenticationHeaderValue CreateAuthorization()
    {
        var encoded = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{SecretKey}:"));

        return new AuthenticationHeaderValue(
            "Basic",
            encoded);
    }

    public async Task<PayMongoCheckoutResult>
        CreateGcashCheckoutSessionAsync(
            Order order,
            OrderFinancial financial,
            Payment payment,
            CancellationToken cancellationToken)
    {
        if (!string.Equals(
                financial.Currency,
                "PHP",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "PayMongo GCash checkout requires PHP.");
        }

        if (financial.TotalAmount <= 0)
        {
            throw new InvalidOperationException(
                "Payment amount must be greater than zero.");
        }

        // PayMongo amounts use the smallest currency unit.
        // PHP 1.00 becomes 100 centavos.
        var amountInCentavos = checked(
            (long)Math.Round(
                financial.TotalAmount * 100m,
                0,
                MidpointRounding.AwayFromZero));

        var payload = new
        {
            data = new
            {
                attributes = new
                {
                    billing = new
                    {
                        name = order.CustomerName
                    },
                    send_email_receipt = false,
                    show_description = true,
                    show_line_items = true,
                    description =
                        $"Alpha order {order.OrderNumber}",
                    payment_method_types = new[]
                    {
                        "gcash"
                    },
                    line_items = new[]
                    {
                        new
                        {
                            currency = "PHP",
                            amount = amountInCentavos,
                            name = $"Alpha Order {order.OrderNumber}",
                            quantity = 1,
                            description = order.ItemDescription
                        }
                    },
                    success_url =
                        $"{FrontendBaseUrl}/customer/payment/paymongo/success" +
                        $"?orderId={order.Id}",
                    cancel_url =
                        $"{FrontendBaseUrl}/customer/payment/paymongo/cancel" +
                        $"?orderId={order.Id}",
                    metadata = new Dictionary<string, string>
                    {
                        ["alpha_order_id"] = order.Id.ToString(),
                        ["alpha_order_number"] = order.OrderNumber,
                        ["alpha_payment_id"] = payment.Id.ToString()
                    }
                }
            }
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{BaseUrl}/checkout_sessions");

        request.Headers.Authorization = CreateAuthorization();

        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        using var response = await _http.SendAsync(
            request,
            cancellationToken);

        var json = await response.Content.ReadAsStringAsync(
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"PayMongo checkout error: {json}");
        }

        using var document = JsonDocument.Parse(json);

        var data = document.RootElement.GetProperty("data");
        var attributes = data.GetProperty("attributes");

        var checkoutSessionId =
            data.GetProperty("id").GetString()
            ?? throw new InvalidOperationException(
                "PayMongo did not return a checkout session ID.");

        var checkoutUrl =
            attributes.GetProperty("checkout_url").GetString()
            ?? throw new InvalidOperationException(
                "PayMongo did not return a checkout URL.");

        return new PayMongoCheckoutResult
        {
            CheckoutSessionId = checkoutSessionId,
            CheckoutUrl = checkoutUrl,
            RawResponse = json
        };
    }

    public async Task<JsonDocument> RetrieveCheckoutSessionAsync(
        string checkoutSessionId,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{BaseUrl}/checkout_sessions/{checkoutSessionId}");

        request.Headers.Authorization = CreateAuthorization();

        using var response = await _http.SendAsync(
            request,
            cancellationToken);

        var json = await response.Content.ReadAsStringAsync(
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"PayMongo retrieval error: {json}");
        }

        return JsonDocument.Parse(json);
    }
}

public class PayMongoCheckoutResult
{
    public string CheckoutSessionId { get; set; } = string.Empty;
    public string CheckoutUrl { get; set; } = string.Empty;
    public string RawResponse { get; set; } = string.Empty;
}