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
    private readonly PaymentCompletionService _paymentCompletionService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StripeWebhookController> _logger;

    public StripeWebhookController(
        AppDbContext context,
        PaymentCompletionService paymentCompletionService,
        IConfiguration configuration,
        ILogger<StripeWebhookController> logger)
    {
        _context = context;
        _paymentCompletionService = paymentCompletionService;
        _configuration = configuration;
        _logger = logger;
    }

    // ============================================================
    // STRIPE WEBHOOK ENTRY POINT
    // POST /api/webhooks/stripe
    // ============================================================

    [HttpPost]
    public async Task<IActionResult> Receive(
        CancellationToken cancellationToken)
    {
        string json;

        // --------------------------------------------------------
        // 1. Read the raw Stripe request body
        // --------------------------------------------------------

        using (var reader = new StreamReader(Request.Body))
        {
            json = await reader.ReadToEndAsync(cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            _logger.LogWarning(
                "Stripe webhook received an empty request body.");

            return BadRequest("Empty request body.");
        }

        // --------------------------------------------------------
        // 2. Get Stripe signature
        // --------------------------------------------------------

        var signature =
            Request.Headers["Stripe-Signature"]
                .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(signature))
        {
            _logger.LogWarning(
                "Stripe-Signature header is missing.");

            return BadRequest(
                "Stripe-Signature header missing.");
        }

        // --------------------------------------------------------
        // 3. Get webhook signing secret
        // --------------------------------------------------------

        var webhookSecret =
            _configuration["STRIPE_WEBHOOK_SECRET"];

        if (string.IsNullOrWhiteSpace(webhookSecret))
        {
            _logger.LogError(
                "STRIPE_WEBHOOK_SECRET is missing.");

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                "Stripe webhook secret is not configured.");
        }

        // --------------------------------------------------------
        // 4. Verify Stripe signature
        // --------------------------------------------------------

        Event stripeEvent;

        try
        {
            stripeEvent =
                EventUtility.ConstructEvent(
                    json,
                    signature,
                    webhookSecret);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(
                ex,
                "Stripe webhook signature validation failed.");

            return BadRequest(
                "Invalid Stripe webhook signature.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Unable to construct Stripe webhook event.");

            return BadRequest(
                "Invalid Stripe webhook.");
        }

        // --------------------------------------------------------
        // 5. Log the received event
        // --------------------------------------------------------

        _logger.LogInformation(
            "Stripe webhook received. EventId={EventId}, Type={EventType}",
            stripeEvent.Id,
            stripeEvent.Type);

        // --------------------------------------------------------
        // 6. Prevent duplicate webhook processing
        // --------------------------------------------------------

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
            _logger.LogInformation(
                "Stripe webhook {EventId} was already processed.",
                stripeEvent.Id);

            return Ok();
        }

        // --------------------------------------------------------
        // 7. Save webhook event before processing
        // --------------------------------------------------------

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

        // --------------------------------------------------------
        // 8. Process event
        // --------------------------------------------------------

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
                        json,
                        cancellationToken);

                    break;

                case "payment_intent.succeeded":

                    await ProcessPaymentIntentSucceeded(
                        stripeEvent,
                        json,
                        cancellationToken);

                    break;

                case "payment_intent.payment_failed":

                    await ProcessPaymentIntentFailed(
                        stripeEvent,
                        json,
                        cancellationToken);

                    break;

                case "charge.refunded":

                    await ProcessChargeRefunded(
                        stripeEvent,
                        json,
                        cancellationToken);

                    break;

                default:

                    _logger.LogInformation(
                        "Unhandled Stripe event: {EventType}",
                        stripeEvent.Type);

                    break;
            }

            // ----------------------------------------------------
            // 9. Mark webhook as processed
            // ----------------------------------------------------

            webhookEvent.Processed = true;

            webhookEvent.ProcessedAt =
                DateTime.UtcNow;

            webhookEvent.ProcessingError = null;

            await _context.SaveChangesAsync(
                cancellationToken);

            return Ok();
        }
        catch (Exception ex)
        {
            // ----------------------------------------------------
            // 10. Record processing error
            // ----------------------------------------------------

            webhookEvent.ProcessingError =
                ex.Message;

            webhookEvent.Processed = false;

            await _context.SaveChangesAsync(
                cancellationToken);

            _logger.LogError(
                ex,
                "Stripe webhook processing failed. " +
                "EventId={EventId}, Type={EventType}",
                stripeEvent.Id,
                stripeEvent.Type);

            // Returning 500 tells Stripe that processing failed
            // and allows Stripe to retry the webhook.
            return StatusCode(
                StatusCodes.Status500InternalServerError);
        }
    }

    // ============================================================
    // CHECKOUT SESSION COMPLETED
    // ============================================================

    private async Task ProcessCheckoutCompleted(
        Event stripeEvent,
        string rawJson,
        CancellationToken cancellationToken)
    {
        var session =
            stripeEvent.Data.Object as Session;

        if (session == null)
        {
            throw new InvalidOperationException(
                "Stripe Checkout Session could not be read.");
        }

        // --------------------------------------------------------
        // Get order_id from Stripe metadata
        // --------------------------------------------------------

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

        // --------------------------------------------------------
        // Find payment
        // --------------------------------------------------------

        var payment =
            await _context.Payments
                .FirstOrDefaultAsync(
                    x => x.OrderId == orderId,
                    cancellationToken);

        if (payment == null)
        {
            throw new InvalidOperationException(
                $"Payment record was not found for order {orderId}.");
        }

        // --------------------------------------------------------
        // Never trust the frontend redirect.
        // Stripe must report the payment as paid.
        // --------------------------------------------------------

        if (!string.Equals(
                session.PaymentStatus,
                "paid",
                StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Stripe Checkout Session {SessionId} " +
                "completed but payment status is {PaymentStatus}.",
                session.Id,
                session.PaymentStatus);

            throw new InvalidOperationException(
                $"Stripe session is not paid. " +
                $"Payment status: {session.PaymentStatus}");
        }

        // --------------------------------------------------------
        // Get transaction reference
        // --------------------------------------------------------

        var transactionReference =
            session.PaymentIntentId
            ?? session.Id;

        // --------------------------------------------------------
        // Complete payment through the existing
        // PaymentCompletionService.
        //
        // This is important because your application already
        // has centralized financial/payment completion logic.
        // --------------------------------------------------------

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

        _logger.LogInformation(
            "Stripe payment completed successfully. " +
            "OrderId={OrderId}, SessionId={SessionId}, " +
            "PaymentIntentId={PaymentIntentId}",
            orderId,
            session.Id,
            session.PaymentIntentId);
    }

    // ============================================================
    // CHECKOUT SESSION EXPIRED
    // ============================================================

    private async Task ProcessCheckoutExpired(
        Event stripeEvent,
        CancellationToken cancellationToken)
    {
        var session =
            stripeEvent.Data.Object as Session;

        if (session == null)
        {
            _logger.LogWarning(
                "Unable to read expired Stripe Checkout Session.");

            return;
        }

        // --------------------------------------------------------
        // Get order_id
        // --------------------------------------------------------

        if (!session.Metadata.TryGetValue(
                "order_id",
                out var orderIdValue))
        {
            _logger.LogWarning(
                "Expired Stripe session {SessionId} " +
                "does not contain order_id.",
                session.Id);

            return;
        }

        if (!Guid.TryParse(
                orderIdValue,
                out var orderId))
        {
            _logger.LogWarning(
                "Invalid order_id in expired Stripe session {SessionId}.",
                session.Id);

            return;
        }

        // --------------------------------------------------------
        // Find payment
        // --------------------------------------------------------

        var payment =
            await _context.Payments
                .FirstOrDefaultAsync(
                    x => x.OrderId == orderId,
                    cancellationToken);

        if (payment == null)
        {
            _logger.LogWarning(
                "Payment not found for expired Stripe order {OrderId}.",
                orderId);

            return;
        }

        // --------------------------------------------------------
        // Never change an already-paid payment
        // --------------------------------------------------------

        if (string.Equals(
                payment.PaymentStatus,
                "paid",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        payment.PaymentStatus =
            "expired";

        payment.FailureReason =
            "Stripe Checkout Session expired.";

        // --------------------------------------------------------
        // Save Stripe session response
        // --------------------------------------------------------

        payment.GatewayPaymentId =
            session.PaymentIntentId;

        payment.GatewayCheckoutSessionId =
            session.Id;

        await _context.SaveChangesAsync(
            cancellationToken);

        _logger.LogInformation(
            "Stripe Checkout Session expired. " +
            "OrderId={OrderId}, SessionId={SessionId}",
            orderId,
            session.Id);
    }

    // ============================================================
    // ASYNC PAYMENT FAILED
    // ============================================================

    private async Task ProcessCheckoutPaymentFailed(
     Event stripeEvent,
     string rawJson,
     CancellationToken cancellationToken)
    {
        var session =
            stripeEvent.Data.Object as Session;

        if (session == null)
        {
            return;
        }

        // --------------------------------------------------------
        // Get order_id
        // --------------------------------------------------------

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

        // --------------------------------------------------------
        // Find payment
        // --------------------------------------------------------

        var payment =
            await _context.Payments
                .FirstOrDefaultAsync(
                    x => x.OrderId == orderId,
                    cancellationToken);

        if (payment == null)
        {
            return;
        }

        // --------------------------------------------------------
        // Never overwrite successful payment
        // --------------------------------------------------------

        if (string.Equals(
                payment.PaymentStatus,
                "paid",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        payment.PaymentStatus =
            "failed";

        payment.FailureReason =
            "Stripe payment failed.";

        payment.GatewayPaymentId =
            session.PaymentIntentId;

        payment.GatewayCheckoutSessionId =
            session.Id;

        payment.GatewayResponse =
     JsonDocument.Parse(rawJson);

        await _context.SaveChangesAsync(
            cancellationToken);

        _logger.LogWarning(
            "Stripe payment failed. " +
            "OrderId={OrderId}, SessionId={SessionId}",
            orderId,
            session.Id);
    }

    // ============================================================
    // PAYMENT INTENT SUCCEEDED
    // ============================================================

    private async Task ProcessPaymentIntentSucceeded(
        Event stripeEvent,
        string rawJson,
        CancellationToken cancellationToken)
    {
        var paymentIntent =
            stripeEvent.Data.Object as PaymentIntent;

        if (paymentIntent == null)
        {
            return;
        }

        // --------------------------------------------------------
        // Find order_id from metadata
        // --------------------------------------------------------

        if (!paymentIntent.Metadata.TryGetValue(
                "order_id",
                out var orderIdValue))
        {
            _logger.LogInformation(
                "Stripe PaymentIntent {PaymentIntentId} " +
                "has no order_id metadata.",
                paymentIntent.Id);

            return;
        }

        if (!Guid.TryParse(
                orderIdValue,
                out var orderId))
        {
            _logger.LogWarning(
                "Invalid order_id in PaymentIntent {PaymentIntentId}.",
                paymentIntent.Id);

            return;
        }

        // --------------------------------------------------------
        // Find payment
        // --------------------------------------------------------

        var payment =
            await _context.Payments
                .FirstOrDefaultAsync(
                    x => x.OrderId == orderId,
                    cancellationToken);

        if (payment == null)
        {
            _logger.LogWarning(
                "Payment record not found for order {OrderId}.",
                orderId);

            return;
        }

        // --------------------------------------------------------
        // Protect against duplicate completion
        // --------------------------------------------------------

        if (string.Equals(
                payment.PaymentStatus,
                "paid",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // --------------------------------------------------------
        // Complete payment
        // --------------------------------------------------------

        await _paymentCompletionService
            .CompleteOrderPaymentAsync(
                orderId: orderId,

                gateway: "stripe",

                paymentMethod: "stripe",

                transactionReference:
                    paymentIntent.Id,

                gatewayPaymentId:
                    paymentIntent.Id,

                rawGatewayResponse:
                    rawJson,

                gatewayFee:
                    0m,

                cancellationToken:
                    cancellationToken);

        _logger.LogInformation(
            "Stripe PaymentIntent completed. " +
            "OrderId={OrderId}, PaymentIntentId={PaymentIntentId}",
            orderId,
            paymentIntent.Id);
    }

    // ============================================================
    // PAYMENT INTENT FAILED
    // ============================================================

    private async Task ProcessPaymentIntentFailed(
     Event stripeEvent,
     string rawJson,
     CancellationToken cancellationToken)
    {
        var paymentIntent =
            stripeEvent.Data.Object as PaymentIntent;

        if (paymentIntent == null)
        {
            return;
        }

        // --------------------------------------------------------
        // Get order_id from Stripe metadata
        // --------------------------------------------------------

        if (!paymentIntent.Metadata.TryGetValue(
                "order_id",
                out var orderIdValue))
        {
            _logger.LogInformation(
                "Stripe PaymentIntent {PaymentIntentId} " +
                "has no order_id metadata.",
                paymentIntent.Id);

            return;
        }

        if (!Guid.TryParse(
                orderIdValue,
                out var orderId))
        {
            _logger.LogWarning(
                "Invalid order_id in PaymentIntent {PaymentIntentId}.",
                paymentIntent.Id);

            return;
        }

        // --------------------------------------------------------
        // Find payment
        // --------------------------------------------------------

        var payment =
            await _context.Payments
                .FirstOrDefaultAsync(
                    x => x.OrderId == orderId,
                    cancellationToken);

        if (payment == null)
        {
            _logger.LogWarning(
                "Payment record not found for order {OrderId}.",
                orderId);

            return;
        }

        // --------------------------------------------------------
        // Never overwrite successful payment
        // --------------------------------------------------------

        if (string.Equals(
                payment.PaymentStatus,
                "paid",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // --------------------------------------------------------
        // Mark payment failed
        // --------------------------------------------------------

        payment.PaymentStatus = "failed";

        payment.FailureReason ProcessChargeRefunded
            paymentIntent.LastPaymentError?.Message
            ?? "Stripe payment failed.";

        payment.GatewayPaymentId =
            paymentIntent.Id;

        // --------------------------------------------------------
        // Save complete Stripe webhook payload
        // --------------------------------------------------------

        payment.GatewayResponse =
            JsonDocument.Parse(rawJson);

        await _context.SaveChangesAsync(
            cancellationToken);

        _logger.LogWarning(
            "Stripe PaymentIntent failed. " +
            "OrderId={OrderId}, " +
            "PaymentIntentId={PaymentIntentId}, " +
            "Reason={Reason}",
            orderId,
            paymentIntent.Id,
            payment.FailureReason);
    }

    // ============================================================
    // CHARGE REFUNDED
    // ============================================================

    private async Task ProcessChargeRefunded(
    Event stripeEvent,
    string rawJson,
    CancellationToken cancellationToken)
    {
        var charge =
            stripeEvent.Data.Object as Charge;

        if (charge == null)
        {
            return;
        }

        // --------------------------------------------------------
        // Stripe Charge contains the PaymentIntent ID
        // --------------------------------------------------------

        var paymentIntentId =
            charge.PaymentIntentId;

        if (string.IsNullOrWhiteSpace(
                paymentIntentId))
        {
            _logger.LogWarning(
                "Refunded Stripe charge {ChargeId} " +
                "does not have a PaymentIntentId.",
                charge.Id);

            return;
        }

        // --------------------------------------------------------
        // Find our payment
        // --------------------------------------------------------

        var payment =
            await _context.Payments
                .FirstOrDefaultAsync(
                    x =>
                        x.GatewayPaymentId ==
                            paymentIntentId,
                    cancellationToken);

        if (payment == null)
        {
            _logger.LogWarning(
                "Payment not found for refunded " +
                "Stripe PaymentIntent {PaymentIntentId}.",
                paymentIntentId);

            return;
        }

        // --------------------------------------------------------
        // Determine full vs partial refund
        // --------------------------------------------------------

        var amountRefunded =
            charge.AmountRefunded;

        var originalAmount =
            charge.Amount;

        if (originalAmount > 0 &&
            amountRefunded >= originalAmount)
        {
            payment.PaymentStatus =
                "refunded";
        }
        else
        {
            payment.PaymentStatus =
                "partially_refunded";
        }

        // --------------------------------------------------------
        // Save complete Stripe webhook payload
        // --------------------------------------------------------

        payment.GatewayResponse =
            JsonDocument.Parse(rawJson);

        await _context.SaveChangesAsync(
            cancellationToken);

        _logger.LogInformation(
            "Stripe refund processed. " +
            "PaymentIntentId={PaymentIntentId}, " +
            "AmountRefunded={AmountRefunded}",
            paymentIntentId,
            amountRefunded);
    }
}