using Alpha.API.Data;
using Alpha.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Alpha.API.Controllers;

[ApiController]
[Route("api/webhooks/stripe")]
public class StripeWebhookController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly PaymentCompletionService
        _paymentCompletionService;

    private readonly IConfiguration _configuration;

    private readonly ILogger<StripeWebhookController>
        _logger;

    public StripeWebhookController(
        AppDbContext context,
        PaymentCompletionService paymentCompletionService,
        IConfiguration configuration,
        ILogger<StripeWebhookController> logger)
    {
        _context = context;
        _paymentCompletionService =
            paymentCompletionService;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Receive(
        CancellationToken cancellationToken)
    {
        var json =
            await new StreamReader(
                Request.Body)
                .ReadToEndAsync(cancellationToken);

        var signature =
            Request.Headers["Stripe-Signature"]
                .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(signature))
        {
            return BadRequest(
                "Stripe-Signature header missing.");
        }

        var webhookSecret =
            _configuration[
                "STRIPE_WEBHOOK_SECRET"];

        if (string.IsNullOrWhiteSpace(
                webhookSecret))
        {
            _logger.LogError(
                "STRIPE_WEBHOOK_SECRET is missing.");

            return StatusCode(500);
        }

        Event stripeEvent;

        try
        {
            stripeEvent =
                EventUtility.ConstructEvent(
                    json,
                    signature,
                    webhookSecret);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Invalid Stripe webhook.");

            return BadRequest();
        }

        var alreadyProcessed =
            await _context.PaymentWebhookEvents
                .AnyAsync(
                    x =>
                        x.Gateway == "stripe" &&
                        x.GatewayEventId ==
                            stripeEvent.Id,
                    cancellationToken);

        if (alreadyProcessed)
        {
            return Ok();
        }

        var webhookEvent =
            new Models.PaymentWebhookEvent
            {
                Id = Guid.NewGuid(),

                Gateway = "stripe",

                GatewayEventId =
                    stripeEvent.Id,

                EventType =
                    stripeEvent.Type,

                Payload =
                    JsonDocument.Parse(json),

                Processed = false,

                ReceivedAt =
                    DateTime.UtcNow
            };

        _context.PaymentWebhookEvents.Add(
            webhookEvent);

        await _context.SaveChangesAsync(
            cancellationToken);

        try
        {
            switch (stripeEvent.Type)
            {
                case "checkout.session.completed":

                    await ProcessCheckoutCompleted(
                        stripeEvent,
                        json,
                        cancellationToken);

                    break;

                case "checkout.session.async_payment_succeeded":

                    await ProcessCheckoutCompleted(
                        stripeEvent,
                        json,
                        cancellationToken);

                    break;

                case "checkout.session.expired":

                    await ProcessCheckoutExpired(
                        stripeEvent,
                        cancellationToken);

                    break;

                case "checkout.session.async_payment_failed":

                    await ProcessCheckoutPaymentFailed(
                        stripeEvent,
                        cancellationToken);

                    break;

                default:

                    _logger.LogInformation(
                        "Unhandled Stripe event: {EventType}",
                        stripeEvent.Type);

                    break;
            }
        }

            webhookEvent.Processed = true;

            webhookEvent.ProcessedAt =
                DateTime.UtcNow;

            webhookEvent.ProcessingError =
                null;

            await _context.SaveChangesAsync(
                cancellationToken);

            return Ok();
        }
        catch (Exception ex)
        {
            webhookEvent.ProcessingError =
                ex.Message;

            await _context.SaveChangesAsync(
                cancellationToken);

            _logger.LogError(
                ex,
                "Stripe webhook processing failed.");

            return StatusCode(500);
        }
    }

    private async Task ProcessCheckoutCompleted(
        Event stripeEvent,
        string rawJson,
        CancellationToken cancellationToken)
    {
        var session =
            stripeEvent.Data.Object
            as Session;

        if (session == null)
        {
            throw new InvalidOperationException(
                "Stripe Checkout Session could not be read.");
        }

        if (!session.Metadata.TryGetValue(
                "order_id",
                out var orderIdValue))
        {
            throw new InvalidOperationException(
                "order_id was not found in Stripe metadata.");
        }

        if (!Guid.TryParse(
                orderIdValue,
                out var orderId))
        {
            throw new InvalidOperationException(
                "Stripe metadata order_id is invalid.");
        }

        var payment =
            await _context.Payments
                .FirstOrDefaultAsync(
                    x => x.OrderId == orderId,
                    cancellationToken);

        if (payment == null)
        {
            throw new InvalidOperationException(
                "Payment record not found.");
        }

        /*
         * Never trust only the browser redirect.
         *
         * Stripe Checkout reports the actual
         * payment status here.
         */
        if (!string.Equals(
                session.PaymentStatus,
                "paid",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Stripe session is not paid. " +
                $"Payment status: {session.PaymentStatus}");
        }

        var transactionReference =
            session.PaymentIntentId
            ?? session.Id;

        await _paymentCompletionService
            .CompleteOrderPaymentAsync(
                orderId: orderId,

                gateway: "stripe",

                paymentMethod: "stripe",

                transactionReference:
                    transactionReference,

                gatewayPaymentId:
                    session.PaymentIntentId,

                rawGatewayResponse:
                    rawJson,

                gatewayFee:
                    0m,

                cancellationToken:
                    cancellationToken);
    }

    private async Task ProcessCheckoutExpired(
        Event stripeEvent,
        CancellationToken cancellationToken)
    {
        var session =
            stripeEvent.Data.Object
            as Session;

        if (session == null)
        {
            return;
        }

        if (!session.Metadata.TryGetValue(
                "order_id",
                out var orderIdValue))
        {
            return;
        }

        if (!Guid.TryParse(
                orderIdValue,
                out var orderId))
        {
            return;
        }

        var payment =
            await _context.Payments
                .FirstOrDefaultAsync(
                    x => x.OrderId == orderId,
                    cancellationToken);

        if (payment == null)
        {
            return;
        }

        if (payment.PaymentStatus == "paid")
        {
            return;
        }

        payment.PaymentStatus =
            "expired";

        payment.FailureReason =
            "Stripe Checkout Session expired.";

        await _context.SaveChangesAsync(
            cancellationToken);
    }
private async Task ProcessCheckoutPaymentFailed(
    Event stripeEvent,
    CancellationToken cancellationToken)
{
    var session =
        stripeEvent.Data.Object as Session;

    if (session == null)
    {
        return;
    }

    if (!session.Metadata.TryGetValue(
            "order_id",
            out var orderIdValue))
    {
        return;
    }

    if (!Guid.TryParse(
            orderIdValue,
            out var orderId))
    {
        return;
    }

    var payment =
        await _context.Payments
            .FirstOrDefaultAsync(
                x => x.OrderId == orderId,
                cancellationToken);

    if (payment == null)
    {
        return;
    }

    // Never overwrite a successful payment.
    if (payment.PaymentStatus == "paid")
    {
        return;
    }

    payment.PaymentStatus =
        "failed";

    payment.FailureReason =
        "Stripe payment failed.";

    payment.GatewayResponse =
        JsonDocument.Parse(
            stripeEvent.Data.Object.ToJson());

    await _context.SaveChangesAsync(
        cancellationToken);
}
}