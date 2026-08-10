using Alpha.API.Models.Payments;
using System;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Alpha.API.Services.Providers;

public class XenditProvider : IPaymentProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<XenditProvider> _logger;

    private const string BaseUrl =
        "https://api.xendit.co";

    public XenditProvider(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<XenditProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public string Name => "xendit";

    private void AddAuthentication(
        HttpRequestMessage request)
    {
        var secretKey =
            _configuration["XENDIT_SECRET_KEY"];

        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException(
                "XENDIT_SECRET_KEY is missing.");
        }

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(
                        $"{secretKey}:")));
    }

    public async Task<PaymentResult> CreatePaymentAsync(
        PaymentRequest request)
    {
        try
        {
            var frontendUrl =
                _configuration["FRONTEND_URL"];

            var payload = new
            {
                external_id =
                    request.OrderId.ToString(),

                amount =
                    request.Amount,

                payer_email =
                    request.CustomerEmail,

                description =
                    $"Alpha Auto Order {request.OrderId}",

                currency =
                    request.Currency
                        .ToUpperInvariant(),

                success_redirect_url =
                    $"{frontendUrl}/payment/xendit/success" +
                    $"?orderId={request.OrderId}",

                failure_redirect_url =
                    $"{frontendUrl}/payment/xendit/failure" +
                    $"?orderId={request.OrderId}",

                customer = new
                {
                    given_names =
                        request.CustomerName,

                    email =
                        request.CustomerEmail
                }
            };

            var json =
                JsonSerializer.Serialize(payload);

            var client =
                _httpClientFactory.CreateClient();

            using var message =
                new HttpRequestMessage(
                    HttpMethod.Post,
                    $"{BaseUrl}/v2/invoices");

            AddAuthentication(message);

            message.Content =
                new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json");

            message.Headers.Add(
                "X-IDEMPOTENCY-KEY",
                request.OrderId.ToString());

            using var response =
                await client.SendAsync(message);

            var document =
                await PaymentProviderHelper
                    .ReadJsonAsync(response);

            var root =
                document.RootElement;

            var invoiceId =
                PaymentProviderHelper.GetString(
                    root,
                    "id");

            var invoiceUrl =
                PaymentProviderHelper.GetString(
                    root,
                    "invoice_url",
                    "available_payment_methods");

            return new PaymentResult
            {
                Success =
                    !string.IsNullOrWhiteSpace(
                        invoiceId),

                PaymentId =
                    invoiceId ?? "",

                CheckoutUrl =
                    invoiceUrl ?? "",

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
                "Xendit invoice creation failed.");

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
                    $"{BaseUrl}/v2/invoices/{paymentId}");

            AddAuthentication(request);

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
                    "status");

            var externalId =
                PaymentProviderHelper.GetString(
                    root,
                    "external_id");

            return new PaymentStatusResult
            {
                Success = true,

                Status =
                    status ?? "UNKNOWN",

                Reference =
                    externalId ?? paymentId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Xendit status check failed.");

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
                _configuration["XENDIT_SECRET_KEY"];

            var payload = new
            {
                invoice_id = paymentId,

                amount =
                    amount > 0
                        ? amount
                        : (decimal?)null,

                reason =
                    "REQUESTED_BY_CUSTOMER"
            };

            var client =
                _httpClientFactory.CreateClient();

            using var request =
                new HttpRequestMessage(
                    HttpMethod.Post,
                    $"{BaseUrl}/refunds");

            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Basic",
                    Convert.ToBase64String(
                        Encoding.UTF8.GetBytes(
                            $"{secretKey}:")));

            request.Content =
                new StringContent(
                    JsonSerializer.Serialize(payload),
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
                "Xendit refund failed.");

            return false;
        }
    }

    public Task<bool> HandleWebhookAsync(
        HttpRequest request)
    {
        return Task.FromResult(true);
    }
}