using Alpha.API.Models.Payments;
using System;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Alpha.API.Services.Providers;

public class PayPalProvider : IPaymentProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PayPalProvider> _logger;

    public PayPalProvider(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<PayPalProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public string Name => "paypal";

    private string BaseUrl
    {
        get
        {
            var environment =
                _configuration["PAYPAL_ENVIRONMENT"];

            return string.Equals(
                environment,
                "production",
                StringComparison.OrdinalIgnoreCase)
                ? "https://api-m.paypal.com"
                : "https://api-m.sandbox.paypal.com";
        }
    }

    private async Task<string> GetAccessTokenAsync()
    {
        var client =
            _httpClientFactory.CreateClient();

        var clientId =
            _configuration["PAYPAL_CLIENT_ID"];

        var clientSecret =
            _configuration["PAYPAL_CLIENT_SECRET"];

        if (string.IsNullOrWhiteSpace(clientId) ||
            string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException(
                "PayPal credentials are missing.");
        }

        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                $"{BaseUrl}/v1/oauth2/token");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(
                        $"{clientId}:{clientSecret}")));

        request.Content =
            new StringContent(
                "grant_type=client_credentials",
                Encoding.UTF8,
                "application/x-www-form-urlencoded");

        using var response =
            await client.SendAsync(request);

        var document =
            await PaymentProviderHelper
                .ReadJsonAsync(response);

        return document.RootElement
            .GetProperty("access_token")
            .GetString()
            ?? throw new InvalidOperationException(
                "PayPal access token was not returned.");
    }

    public async Task<PaymentResult> CreatePaymentAsync(
        PaymentRequest request)
    {
        try
        {
            var accessToken =
                await GetAccessTokenAsync();

            var client =
                _httpClientFactory.CreateClient();

            var frontendUrl =
                _configuration["FRONTEND_URL"];

            var currency =
                request.Currency
                    .Trim()
                    .ToUpperInvariant();

            var payload = new
            {
                intent = "CAPTURE",

                purchase_units = new[]
                {
                    new
                    {
                        reference_id =
                            request.OrderId.ToString(),

                        description =
                            $"Alpha Auto Order {request.OrderId}",

                        amount = new
                        {
                            currency_code =
                                currency,

                            value =
                                request.Amount
                                    .ToString(
                                        "0.00",
                                        System.Globalization
                                            .CultureInfo
                                            .InvariantCulture)
                        }
                    }
                },

                application_context = new
                {
                    brand_name = "Alpha Auto",

                    user_action =
                        "PAY_NOW",

                    return_url =
                        $"{frontendUrl}/payment/paypal/success" +
                        $"?orderId={request.OrderId}",

                    cancel_url =
                        $"{frontendUrl}/payment/paypal/cancel" +
                        $"?orderId={request.OrderId}"
                }
            };

            var json =
                JsonSerializer.Serialize(payload);

            using var message =
                new HttpRequestMessage(
                    HttpMethod.Post,
                    $"{BaseUrl}/v2/checkout/orders");

            message.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    accessToken);

            message.Headers.Add(
                "PayPal-Request-Id",
                request.OrderId.ToString());

            message.Content =
                new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json");

            using var response =
                await client.SendAsync(message);

            var document =
                await PaymentProviderHelper
                    .ReadJsonAsync(response);

            var root =
                document.RootElement;

            var orderId =
                root.GetProperty("id")
                    .GetString();

            string approvalUrl = "";

            if (root.TryGetProperty(
                    "links",
                    out var links))
            {
                foreach (var link in links.EnumerateArray())
                {
                    if (link.TryGetProperty(
                            "rel",
                            out var rel) &&
                        rel.GetString() == "approve")
                    {
                        approvalUrl =
                            link.GetProperty("href")
                                .GetString()
                            ?? "";

                        break;
                    }
                }
            }

            return new PaymentResult
            {
                Success =
                    !string.IsNullOrWhiteSpace(
                        orderId),

                PaymentId =
                    orderId ?? "",

                CheckoutUrl =
                    approvalUrl,

                TransactionReference =
                    orderId ?? "",

                RawResponse =
                    root.GetRawText()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "PayPal order creation failed.");

            return new PaymentResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    public async Task<PaymentStatusResult> GetStatusAsync(
        string paymentId)
    {
        try
        {
            var token =
                await GetAccessTokenAsync();

            var client =
                _httpClientFactory.CreateClient();

            using var request =
                new HttpRequestMessage(
                    HttpMethod.Get,
                    $"{BaseUrl}/v2/checkout/orders/{paymentId}");

            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    token);

            using var response =
                await client.SendAsync(request);

            var document =
                await PaymentProviderHelper
                    .ReadJsonAsync(response);

            var status =
                document.RootElement
                    .GetProperty("status")
                    .GetString();

            return new PaymentStatusResult
            {
                Success = true,

                Status =
                    status ?? "UNKNOWN",

                Reference =
                    paymentId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "PayPal status check failed.");

            return new PaymentStatusResult
            {
                Success = false,
                Status = "UNKNOWN",
                Reference = paymentId
            };
        }
    }

    public async Task<bool> RefundAsync(
        string paymentId,
        decimal amount)
    {
        // PayPal refunds require the captured
        // payment/capture ID rather than only
        // the order ID.
        //
        // Store the capture ID from the capture
        // response in gateway_payment_id before
        // calling this method.
        await Task.CompletedTask;

        return false;
    }

    public Task<bool> HandleWebhookAsync(
        HttpRequest request)
    {
        return Task.FromResult(true);
    }

    public async Task<bool> CaptureOrderAsync(
        string orderId)
    {
        try
        {
            var token =
                await GetAccessTokenAsync();

            var client =
                _httpClientFactory.CreateClient();

            using var request =
                new HttpRequestMessage(
                    HttpMethod.Post,
                    $"{BaseUrl}/v2/checkout/orders/" +
                    $"{orderId}/capture");

            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    token);

            request.Headers.Add(
                "PayPal-Request-Id",
                Guid.NewGuid().ToString());

            request.Content =
                new StringContent(
                    "{}",
                    Encoding.UTF8,
                    "application/json");

            using var response =
                await client.SendAsync(request);

            var document =
                await PaymentProviderHelper
                    .ReadJsonAsync(response);

            var status =
                document.RootElement
                    .GetProperty("status")
                    .GetString();

            return status == "COMPLETED";
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "PayPal capture failed.");

            return false;
        }
    }
}