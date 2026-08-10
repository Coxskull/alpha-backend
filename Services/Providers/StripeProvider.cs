using Alpha.API.Models.Payments;
using Microsoft.AspNetCore.Http;
using Stripe;
using Stripe.Checkout;
using System;
using System.Collections.Generic;
using System.Text.Json;
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
            request.Currency.Trim().ToLowerInvariant();

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

        var amountInMinorUnits =
            ConvertToMinorUnits(
                request.Amount,
                currency);

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
                        new SessionLineItemOptions
                        {
                            Quantity = 1,

                            PriceData =
                                new SessionLineItemPriceDataOptions
                                {
                                    Currency = currency,

                                    UnitAmount =
                                        amountInMinorUnits,

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

    public async Task<bool> RefundAsync(
        string paymentId,
        decimal amount)
    {
        if (string.IsNullOrWhiteSpace(paymentId))
        {
            return false;
        }

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

    public async Task<bool> HandleWebhookAsync(
        HttpRequest request)
    {
        return true;
    }

    private static long ConvertToMinorUnits(
        decimal amount,
        string currency)
    {
        /*
         * PHP, MXN and USD currently use
         * two decimal places for normal card
         * presentment.
         */

        return checked(
            (long)Math.Round(
                amount * 100m,
                MidpointRounding.AwayFromZero));
    }
}