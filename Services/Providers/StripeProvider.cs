using Alpha.API.Models.Payments;
using Stripe;
using Stripe.Checkout;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Alpha.API.Services.Providers;

public class StripeProvider : IPaymentProvider
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<StripeProvider> _logger;

    public StripeProvider(
        IConfiguration configuration,
        ILogger<StripeProvider> logger)
    {
        _configuration = configuration;
        _logger = logger;

        var secretKey =
            _configuration["STRIPE_SECRET_KEY"];

        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException(
                "STRIPE_SECRET_KEY is missing.");
        }

        StripeConfiguration.ApiKey = secretKey;
    }

    public string Name => "stripe";

    public async Task<PaymentResult> CreatePaymentAsync(
        PaymentRequest request)
    {
        if (request.OrderId == Guid.Empty)
        {
            return new PaymentResult
            {
                Success = false,
                Error = "Order ID is required."
            };
        }

        if (request.Amount <= 0)
        {
            return new PaymentResult
            {
                Success = false,
                Error = "Payment amount must be greater than zero."
            };
        }

        var currency =
            request.Currency
                .Trim()
                .ToLowerInvariant();

        var frontendUrl =
            _configuration["FRONTEND_URL"];

        if (string.IsNullOrWhiteSpace(frontendUrl))
        {
            return new PaymentResult
            {
                Success = false,
                Error = "FRONTEND_URL is missing."
            };
        }

        var successUrl =
            $"{frontendUrl}/customer/orders/{request.OrderId}" +
            "?payment=success" +
            "&session_id={{CHECKOUT_SESSION_ID}}";

        var cancelUrl =
            $"{frontendUrl}/customer/orders/{request.OrderId}" +
            "?payment=cancelled";

        var options =
            new SessionCreateOptions
            {
                Mode = "payment",

                SuccessUrl = successUrl,

                CancelUrl = cancelUrl,

                CustomerEmail =
                    string.IsNullOrWhiteSpace(
                        request.CustomerEmail)
                        ? null
                        : request.CustomerEmail,

                LineItems =
                    new List<SessionLineItemOptions>
                    {
                        new()
                        {
                            Quantity = 1,

                            PriceData =
                                new SessionLineItemPriceDataOptions
                                {
                                    Currency = currency,

                                    UnitAmount =
                                        ConvertToMinorUnits(
                                            request.Amount,
                                            currency),

                                    ProductData =
                                        new SessionLineItemPriceDataProductDataOptions
                                        {
                                            Name =
                                                $"Alpha Auto Order {request.OrderId}"
                                        }
                                }
                        }
                    },

                Metadata =
                    new Dictionary<string, string>
                    {
                        ["order_id"] =
                            request.OrderId.ToString(),

                        ["gateway"] =
                            "stripe"
                    }
            };

        try
        {
            var service =
                new SessionService();

            var session =
                await service.CreateAsync(options);

            return new PaymentResult
            {
                Success = true,

                PaymentId =
                    session.Id,

                CheckoutUrl =
                    session.Url,

                TransactionReference =
                    session.Id,

                RawResponse =
                    session.ToJson()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to create Stripe Checkout Session.");

            return new PaymentResult
            {
                Success = false,

                Error =
                    "Unable to create Stripe Checkout Session."
            };
        }
    }

    public async Task<PaymentStatusResult> GetStatusAsync(
        string paymentId)
    {
        if (string.IsNullOrWhiteSpace(paymentId))
        {
            return new PaymentStatusResult
            {
                Success = false,
                Status = "unknown"
            };
        }

        try
        {
            var service =
                new SessionService();

            var session =
                await service.GetAsync(paymentId);

            return new PaymentStatusResult
            {
                Success = true,

                Status =
                    session.PaymentStatus
                    ?? session.Status
                    ?? "unknown",

                Reference =
                    session.PaymentIntentId
                    ?? session.Id,

                GatewayFee = 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to retrieve Stripe Session.");

            return new PaymentStatusResult
            {
                Success = false,
                Status = "unknown"
            };
        }
    }

    public async Task<bool> RefundAsync(
        string paymentId,
        decimal amount)
    {
        if (string.IsNullOrWhiteSpace(paymentId))
        {
            return false;
        }

        try
        {
            var sessionService =
                new SessionService();

            var session =
                await sessionService.GetAsync(paymentId);

            if (string.IsNullOrWhiteSpace(
                    session.PaymentIntentId))
            {
                return false;
            }

            var refundService =
                new RefundService();

            var options =
                new RefundCreateOptions
                {
                    PaymentIntent =
                        session.PaymentIntentId
                };

            if (amount > 0)
            {
                options.Amount =
                    ConvertToMinorUnits(
                        amount,
                        session.Currency);
            }

            var refund =
                await refundService.CreateAsync(options);

            return refund.Status == "succeeded";
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Stripe refund failed.");

            return false;
        }
    }

    public Task<bool> HandleWebhookAsync(
        HttpRequest request)
    {
        // Stripe webhook verification and processing
        // are handled by StripeWebhookController.
        return Task.FromResult(true);
    }

    private static long ConvertToMinorUnits(
        decimal amount,
        string currency)
    {
        return checked(
            (long)Math.Round(
                amount * 100m,
                MidpointRounding.AwayFromZero));
    }
}