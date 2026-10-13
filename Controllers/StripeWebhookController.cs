using Microsoft.AspNetCore.Authorization;
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
using Alpha.API.Services.Entrepreneur;

namespace Alpha.API.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/webhooks/stripe")]
public class StripeWebhookController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly PaymentCompletionService _paymentCompletionService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StripeWebhookController> _logger;
    private readonly EntrepreneurAdjustmentService _entrepreneurAdjustmentService;

    public StripeWebhookController(
    AppDbContext context,
    PaymentCompletionService paymentCompletionService,
    IConfiguration configuration,
    ILogger<StripeWebhookController> logger,
    EntrepreneurAdjustmentService entrepreneurAdjustmentService)
    {
        _context = context;
        _paymentCompletionService =
            paymentCompletionService;

        _configuration = configuration;
        _logger = logger;

        _entrepreneurAdjustmentService =
            entrepreneurAdjustmentService;
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

        var lease = await new WebhookClaimService(_context).ClaimAsync(
            "stripe", stripeEvent.Id, stripeEvent.Type, json, cancellationToken);
        var webhookEvent = lease.Event;
        if (webhookEvent.Processed) return Ok();
        if (!lease.Claimed)
        {
            Response.Headers.RetryAfter = "30";
            return StatusCode(503);
        }

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
            webhookEvent.ProcessingUntil = null;

            webhookEvent.ProcessedAt =
                DateTime.UtcNow;

            webhookEvent.ProcessingError = null;

            await _context.SaveChangesAsync(
                cancellationToken);

            return Ok();
        }
        catch (Exception ex)
        {
            var failedEventId = webhookEvent.Id;
            _context.ChangeTracker.Clear();
            webhookEvent = await _context.PaymentWebhookEvents.SingleAsync(e => e.Id == failedEventId, CancellationToken.None);

            // ----------------------------------------------------
            // 10. Record processing error
            // ----------------------------------------------------

            webhookEvent.ProcessingError = "processing_failed";

            webhookEvent.Processed = false;
            webhookEvent.ProcessingUntil = null;

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

        payment.FailureReason =
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
            return;

        var paymentIntentId =
            charge.PaymentIntentId;

        if (string.IsNullOrWhiteSpace(
                paymentIntentId))
        {
            return;
        }

        await using var refundTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM payments WHERE gateway_payment_id = {paymentIntentId} OR order_id IN (SELECT order_id FROM payments WHERE gateway_payment_id = {paymentIntentId}) ORDER BY id FOR UPDATE", cancellationToken);

        var matches = await _context.Payments.Where(p => p.GatewayPaymentId == paymentIntentId).Take(2).ToListAsync(cancellationToken);
        if (matches.Count == 0) return;
        if (matches.Count != 1) throw new InvalidOperationException("Stripe payment mapping is ambiguous and requires manual review.");
        var payment = matches[0];

        await _context.Entry(payment).ReloadAsync(cancellationToken);
        if (!string.Equals(payment.PaymentGateway, "stripe", StringComparison.OrdinalIgnoreCase)
            || payment.PaymentStatus is not ("paid" or "partially_refunded" or "refunded")
            || (payment.RefundedAmount > 0 && payment.RefundReference != charge.Id)
            || !StripeRefundEvidenceValidator.SupportedCurrency(payment.Currency)
            || charge.Livemode != stripeEvent.Livemode
            || charge.Currency.ToUpperInvariant() != payment.Currency || charge.Amount / 100m != payment.Amount
            || charge.AmountRefunded < 0 || charge.AmountRefunded > charge.Amount)
            throw new InvalidOperationException("Stripe refund amount or currency did not match the payment.");

        var originalAmount =
            charge.Amount / 100m;

        var cumulativeRefund =
            charge.AmountRefunded / 100m;

        var previousRefund =
            payment.RefundedAmount;

        var refundDelta =
            Math.Max(
                0m,
                cumulativeRefund -
                previousRefund);

        // Late complete evidence may arrive after an earlier cumulative-only event.
        // Register IDs even when the cumulative total is unchanged; never regress totals.
        using var proof = JsonDocument.Parse(rawJson);
        var completeListDeclared = proof.RootElement.TryGetProperty("data", out var data)
            && data.TryGetProperty("object", out var body)
            && body.TryGetProperty("refunds", out var list) && list.ValueKind == JsonValueKind.Object
            && list.TryGetProperty("has_more", out var hasMore) && hasMore.ValueKind == JsonValueKind.False;
        var evidence = completeListDeclared
            ? await new StripeRefundOperationService(_context).RegisterAsync(payment, charge, cancellationToken)
            : StripeRefundEvidenceState.Incomplete;
        var currentCompleteEvidence = evidence == StripeRefundEvidenceState.Verified && cumulativeRefund >= previousRefund;
        if (evidence == StripeRefundEvidenceState.Conflict) payment.RefundEvidenceConflict = true;

        /*
         * ---------------------------------------------------------
         * PAYMENT REFUND
         * ---------------------------------------------------------
         */

        if (refundDelta > 0m)
        {
            payment.RefundedAmount =
                cumulativeRefund;

            payment.RefundStatus =
                cumulativeRefund >= originalAmount
                    ? "full"
                    : "partial";

            payment.RefundReference =
                charge.Id;

            payment.RefundedAt =
                DateTime.UtcNow;

            payment.GatewayResponse =
                JsonDocument.Parse(rawJson);

            payment.PaymentStatus =
                cumulativeRefund >= originalAmount
                    ? "refunded"
                    : "partially_refunded";
        }

        /*
         * ---------------------------------------------------------
         * ENTREPRENEUR EARNING
         * ---------------------------------------------------------
         */

        // Completed individual refunds can be allocated by an administrator; cumulative-only evidence cannot.
        // Preserve confirmed customer refund totals but hold source-order payouts for review.
        // Linearize evidence-state changes against concurrent recovery of existing claims.
        var affectedPayees = await (from allocation in _context.Set<Alpha.API.Models.RefundAllocation>()
            join operation in _context.Set<Alpha.API.Models.PaymentOperation>() on allocation.OperationId equals operation.Id
            join sourcePayment in _context.Payments on operation.PaymentId equals sourcePayment.Id
            where sourcePayment.OrderId == payment.OrderId && allocation.FutureRecovery && allocation.RecoveredAmount < allocation.Amount
            select new { allocation.PayeeType, allocation.PayeeId, allocation.Currency }).Distinct()
            .OrderBy(a => a.PayeeType).ThenBy(a => a.PayeeId).ThenBy(a => a.Currency).ToListAsync(cancellationToken);
        var recovery = new RefundAllocationService(_context);
        foreach (var payee in affectedPayees)
            await recovery.LockPayeeAsync(payee.PayeeType, payee.PayeeId, payee.Currency, cancellationToken);
        var financial = await _context.OrderFinancials.FirstOrDefaultAsync(f => f.OrderId == payment.OrderId, cancellationToken);
        if (financial != null)
        {
            if (financial.Currency != payment.Currency || await _context.Payments.AnyAsync(
                p => p.OrderId == payment.OrderId && p.RefundedAmount > 0 && p.Currency != financial.Currency, cancellationToken))
                throw new InvalidOperationException("Refund currencies require manual financial review.");
            financial.RefundAmount = payment.RefundedAmount + await _context.Payments
                .Where(p => p.OrderId == payment.OrderId && p.Id != payment.Id).SumAsync(p => p.RefundedAmount, cancellationToken);
            if (financial.FinancialStatus == RefundConflictGuard.Status || payment.RefundEvidenceConflict
                || await RefundConflictGuard.HasAsync(_context, payment.OrderId, cancellationToken)) financial.FinancialStatus = RefundConflictGuard.Status;
            else if (currentCompleteEvidence)
                financial.FinancialStatus = refundDelta == 0 && financial.FinancialStatus == "refund_allocated_review" ? "refund_allocated_review" : "refund_review";
            else if (financial.FinancialStatus != "refund_evidence_conflict" && cumulativeRefund >= previousRefund
                && (refundDelta > 0 || financial.FinancialStatus is not ("refund_review" or "refund_allocated_review")))
                financial.FinancialStatus = "refund_evidence_review";
            financial.PayoutStatus = "on_hold";
            financial.SettlementStatus = "blocked";
            var queues = await _context.SettlementQueue.Where(q => q.OrderFinancialId == financial.Id && q.Status != "paid").ToListAsync(cancellationToken);
            foreach (var queue in queues) queue.Status = "on_hold";
        }

        if (evidence == StripeRefundEvidenceState.Conflict && payment.OrderId.HasValue)
            _context.AuditLogs.Add(new Alpha.API.Models.AuditLog { Id = Guid.NewGuid(), OrderId = payment.OrderId.Value,
                Action = "Stripe refund evidence conflict held for manual review", PerformedBy = "stripe", CreatedAt = DateTime.UtcNow });

        await _context.SaveChangesAsync(
            cancellationToken);

        await refundTransaction.CommitAsync(cancellationToken);

        _logger.LogInformation("Stripe refund recorded for attribution review. PaymentIntent={PaymentIntentId}, RefundDelta={RefundDelta}", paymentIntentId, refundDelta);
    }
}
