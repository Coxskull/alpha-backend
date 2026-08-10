using Alpha.API.Models.Payments;
using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Alpha.API.Services.Providers;

public class MayaProvider : IPaymentProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MayaProvider> _logger;

    public MayaProvider(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<MayaProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public string Name => "maya";

    private string BaseUrl
    {
        get
        {
            var environment =
                _configuration["MAYA_ENVIRONMENT"];

            return string.Equals(
                environment,
                "production",
                StringComparison.OrdinalIgnoreCase)
                ? "https://pg.paymaya.com"
                : "https://pg-sandbox.paymaya.com";
        }
    }

    public async Task<PaymentResult> CreatePaymentAsync(
        PaymentRequest request)
    {
        try
        {
            var publicKey =
                _configuration["MAYA_PUBLIC_KEY"];

            if (string.IsNullOrWhiteSpace(publicKey))
            {
                throw new InvalidOperationException(
                    "MAYA_PUBLIC_KEY is missing.");
            }

            var frontendUrl =
                _configuration["FRONTEND_URL"];

            var payload = new
            {
                totalAmount = new
                {
                    value =
                        request.Amount,

                    currency =
                        request.Currency.ToUpperInvariant()
                },

                buyer = new
                {
                    firstName =
                        request.CustomerName,

                    email =
                        request.CustomerEmail
                },

                items = new[]
                {
                    new
                    {
                        name =
                            $"Alpha Auto Order {request.OrderId}",

                        quantity = 1,

                        totalAmount = new
                        {
                            value =
                                request.Amount,

                            currency =
                                request.Currency
                                    .ToUpperInvariant()
                        }
                    }
                },

                redirectUrl = new
                {
                    success =
                        $"{frontendUrl}/payment/maya/success" +
                        $"?orderId={request.OrderId}",

                    failure =
                        $"{frontendUrl}/payment/maya/failure" +
                        $"?orderId={request.OrderId}",

                    cancel =
                        $"{frontendUrl}/payment/maya/cancel" +
                        $"?orderId={request.OrderId}"
                },

                requestReferenceNumber =
                    request.OrderId.ToString(),

                metadata = new
                {
                    order_id =
                        request.OrderId.ToString()
                }
            };

            var json =
                JsonSerializer.Serialize(payload);

            var client =
                _httpClientFactory.CreateClient();

            using var message =
                new HttpRequestMessage(
                    HttpMethod.Post,
                    $"{BaseUrl}/checkout/v1/checkouts");

            message.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue(
                    "Basic",
                    Convert.ToBase64String(
                        Encoding.UTF8.GetBytes(
                            $"{publicKey}:")));

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

            var checkoutId =
                root.GetProperty("checkoutId")
                    .GetString();

            var redirectUrl =
                root.GetProperty("redirectUrl")
                    .GetString();

            return new PaymentResult
            {
                Success =
                    !string.IsNullOrWhiteSpace(
                        redirectUrl),

                PaymentId =
                    checkoutId ?? "",

                CheckoutUrl =
                    redirectUrl ?? "",

                TransactionReference =
                    request.OrderId.ToString(),

                RawResponse =
                    root.GetRawText()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Maya Checkout creation failed.");

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
            var secretKey =
                _configuration["MAYA_SECRET_KEY"];

            var client =
                _httpClientFactory.CreateClient();

            using var request =
                new HttpRequestMessage(
                    HttpMethod.Get,
                    $"{BaseUrl}/payments/v1/payments/{paymentId}");

            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue(
                    "Basic",
                    Convert.ToBase64String(
                        Encoding.UTF8.GetBytes(
                            $"{secretKey}:")));

            using var response =
                await client.SendAsync(request);

            var document =
                await PaymentProviderHelper
                    .ReadJsonAsync(response);

            var root =
                document.RootElement;

            var status =
                PaymentProviderHelper.GetString(
                    root,
                    "status",
                    "paymentStatus");

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
                "Maya status check failed.");

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
        try
        {
            var secretKey =
                _configuration["MAYA_SECRET_KEY"];

            var client =
                _httpClientFactory.CreateClient();

            using var request =
                new HttpRequestMessage(
                    HttpMethod.Post,
                    $"{BaseUrl}/payments/v1/payments/" +
                    $"{paymentId}/cancel");

            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue(
                    "Basic",
                    Convert.ToBase64String(
                        Encoding.UTF8.GetBytes(
                            $"{secretKey}:")));

            request.Content =
                new StringContent(
                    "{}",
                    Encoding.UTF8,
                    "application/json");

            using var response =
                await client.SendAsync(request);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Maya cancellation failed.");

            return false;
        }
    }

    public Task<bool> HandleWebhookAsync(
        HttpRequest request)
    {
        return Task.FromResult(true);
    }
}