using Alpha.API.Models.Payments;
using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Alpha.API.Services.Providers;

public class PayMongoProvider : IPaymentProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PayMongoProvider> _logger;

    public PayMongoProvider(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<PayMongoProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public string Name => "paymongo";

    public async Task<PaymentResult> CreatePaymentAsync(
        PaymentRequest request)
    {
        try
        {
            var secretKey =
                _configuration["PAYMONGO_SECRET_KEY"];

            if (string.IsNullOrWhiteSpace(secretKey))
            {
                throw new InvalidOperationException(
                    "PAYMONGO_SECRET_KEY is missing.");
            }

            var frontendUrl =
                _configuration["FRONTEND_URL"];

            if (string.IsNullOrWhiteSpace(frontendUrl))
            {
                throw new InvalidOperationException(
                    "FRONTEND_URL is missing.");
            }

            var currency =
                request.Currency
                    .Trim()
                    .ToUpperInvariant();

            if (currency != "PHP")
            {
                return new PaymentResult
                {
                    Success = false,
                    Error =
                        "PayMongo Checkout is configured " +
                        "for PHP in this integration."
                };
            }

            var client =
                _httpClientFactory.CreateClient();

            var payload = new
            {
                data = new
                {
                    attributes = new
                    {
                        line_items = new[]
                        {
                            new
                            {
                                name =
                                    $"Alpha Auto Order {request.OrderId}",

                                amount =
                                    PaymentProviderHelper
                                        .ToMinorUnits(
                                            request.Amount),

                                currency = "PHP",

                                quantity = 1
                            }
                        },

                        payment_method_types =
                            new[]
                            {
                                "card",
                                "gcash",
                                "qrph"
                            },

                        success_url =
                            $"{frontendUrl}/payment/success" +
                            $"?orderId={request.OrderId}" +
                            $"&gateway=paymongo",

                        cancel_url =
                            $"{frontendUrl}/payment/cancel" +
                            $"?orderId={request.OrderId}" +
                            $"&gateway=paymongo",

                        reference_number =
                            request.OrderId.ToString(),

                        description =
                            $"Alpha Auto Order {request.OrderId}"
                    }
                }
            };

            var json =
                JsonSerializer.Serialize(payload);

            using var message =
                new HttpRequestMessage(
                    HttpMethod.Post,
                    "https://api.paymongo.com/v2/checkout_sessions");

            message.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue(
                    "Basic",
                    Convert.ToBase64String(
                        Encoding.UTF8.GetBytes(
                            $"{secretKey}:")));

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

            var data =
                root.GetProperty("data");

            var id =
                data.GetProperty("id")
                    .GetString();

            var attributes =
                data.GetProperty("attributes");

            var checkoutUrl =
                attributes
                    .GetProperty("checkout_url")
                    .GetString();

            return new PaymentResult
            {
                Success =
                    !string.IsNullOrWhiteSpace(
                        checkoutUrl),

                PaymentId =
                    id ?? "",

                CheckoutUrl =
                    checkoutUrl ?? "",

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
                "PayMongo payment creation failed.");

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
                _configuration["PAYMONGO_SECRET_KEY"];

            var client =
                _httpClientFactory.CreateClient();

            using var message =
                new HttpRequestMessage(
                    HttpMethod.Get,
                    $"https://api.paymongo.com/v1/checkout_sessions/{paymentId}");

            message.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue(
                    "Basic",
                    Convert.ToBase64String(
                        Encoding.UTF8.GetBytes(
                            $"{secretKey}:")));

            using var response =
                await client.SendAsync(message);

            var document =
                await PaymentProviderHelper
                    .ReadJsonAsync(response);

            var attributes =
                document.RootElement
                    .GetProperty("data")
                    .GetProperty("attributes");

            var paymentStatus =
                PaymentProviderHelper.GetString(
                    attributes,
                    "payment_status",
                    "status");

            return new PaymentStatusResult
            {
                Success = true,

                Status =
                    paymentStatus ?? "unknown",

                Reference =
                    paymentId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "PayMongo status check failed.");

            return new PaymentStatusResult
            {
                Success = false,
                Status = "unknown",
                Reference = paymentId
            };
        }
    }

    public Task<bool> RefundAsync(
        string paymentId,
        decimal amount)
    {
        // Refunds should be handled from the PayMongo
        // Payment/Refund API using the actual payment ID.
        //
        // We intentionally do not fake a successful refund here.
        return Task.FromResult(false);
    }

    public async Task<bool> HandleWebhookAsync(
        HttpRequest request)
    {
        // Webhook verification and payment completion
        // should be implemented in your PayMongoWebhookController.
        await Task.CompletedTask;

        return true;
    }
}