using Alpha.API.Models.Payments;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace Alpha.API.Services.Providers;

public class HitPayProvider : IPaymentProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HitPayProvider> _logger;

    public HitPayProvider(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<HitPayProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public string Name => "hitpay";

    private string BaseUrl
    {
        get
        {
            var environment =
                _configuration["HITPAY_ENVIRONMENT"];

            return string.Equals(
                environment,
                "production",
                StringComparison.OrdinalIgnoreCase)
                ? "https://api.hit-pay.com"
                : "https://api.sandbox.hit-pay.com";
        }
    }

    private string ApiKey =>
        _configuration["HITPAY_API_KEY"]
        ?? throw new InvalidOperationException(
            "HITPAY_API_KEY is missing.");

    public async Task<PaymentResult> CreatePaymentAsync(
        PaymentRequest request)
    {
        try
        {
            var frontendUrl =
                _configuration["FRONTEND_URL"];

            var client =
                _httpClientFactory.CreateClient();

            using var message =
                new HttpRequestMessage(
                    HttpMethod.Post,
                    $"{BaseUrl}/v1/payment-requests");

            message.Headers.Add(
                "X-BUSINESS-API-KEY",
                ApiKey);

            var form =
                new List<KeyValuePair<string, string>>
                {
                    new(
                        "amount",
                        request.Amount
                            .ToString(
                                "0.00",
                                System.Globalization
                                    .CultureInfo
                                    .InvariantCulture)),

                    new(
                        "currency",
                        request.Currency
                            .ToUpperInvariant()),

                    new(
                        "email",
                        request.CustomerEmail),

                    new(
                        "name",
                        request.CustomerName),

                    new(
                        "purpose",
                        $"Alpha Auto Order {request.OrderId}"),

                    new(
                        "reference_number",
                        request.OrderId.ToString()),

                    new(
                        "redirect_url",
                        $"{frontendUrl}/payment/hitpay/success" +
                        $"?orderId={request.OrderId}"),

                    new(
                        "webhook",
                        $"{frontendUrl}/api/webhooks/hitpay"),

                    new(
                        "allow_repeated_payments",
                        "false")
                };

            message.Content =
                new FormUrlEncodedContent(form);

            using var response =
                await client.SendAsync(message);

            var document =
                await PaymentProviderHelper
                    .ReadJsonAsync(response);

            var root =
                document.RootElement;

            var paymentId =
                root.GetProperty("id")
                    .GetString();

            var checkoutUrl =
                root.GetProperty("url")
                    .GetString();

            return new PaymentResult
            {
                Success =
                    !string.IsNullOrWhiteSpace(
                        checkoutUrl),

                PaymentId =
                    paymentId ?? "",

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
                "HitPay payment creation failed.");

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
            var client =
                _httpClientFactory.CreateClient();

            using var request =
                new HttpRequestMessage(
                    HttpMethod.Get,
                    $"{BaseUrl}/v1/payment-requests/{paymentId}");

            request.Headers.Add(
                "X-BUSINESS-API-KEY",
                ApiKey);

            using var response =
                await client.SendAsync(request);

            var document =
                await PaymentProviderHelper
                    .ReadJsonAsync(response);

            var root =
                document.RootElement;

            var status =
                root.GetProperty("status")
                    .GetString();

            var reference =
                PaymentProviderHelper.GetString(
                    root,
                    "reference_number");

            return new PaymentStatusResult
            {
                Success = true,

                Status =
                    status ?? "UNKNOWN",

                Reference =
                    reference ?? paymentId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "HitPay status check failed.");

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
        // HitPay refund implementation depends
        // on the specific payment method and
        // refund endpoint enabled for your account.
        await Task.CompletedTask;

        return false;
    }

    public Task<bool> HandleWebhookAsync(
        HttpRequest request)
    {
        return Task.FromResult(true);
    }
}