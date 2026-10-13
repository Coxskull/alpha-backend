using Microsoft.AspNetCore.Authorization;
using Alpha.API.Data;
using Alpha.API.Models;
using Alpha.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Alpha.API.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/webhooks/paymongo")]
public class PayMongoWebhookController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly PayMongoService _payMongo;
    private readonly PaymentCompletionService
        _paymentCompletionService;
    private readonly ILogger<PayMongoWebhookController>
        _logger;

    public PayMongoWebhookController(
        AppDbContext context,
        PayMongoService payMongo,
        PaymentCompletionService paymentCompletionService,
        ILogger<PayMongoWebhookController> logger,
        IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
        _payMongo = payMongo;
        _paymentCompletionService =
            paymentCompletionService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Receive(
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(
            Request.Body);

        var rawBody =
            await reader.ReadToEndAsync(
                cancellationToken);

        if (string.IsNullOrWhiteSpace(rawBody))
            return BadRequest();

        var secret = _configuration["PayMongo:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(secret)) return StatusCode(503);
        if (!Alpha.API.Security.PayMongoSignature.Verify(rawBody,
            Request.Headers["Paymongo-Signature"].ToString(), secret,
            _configuration.GetValue<bool>("PayMongo:LiveMode"), DateTimeOffset.UtcNow)) return BadRequest();

        using var document =
            JsonDocument.Parse(rawBody);

        var root = document.RootElement;
        var eventData = root.GetProperty("data");

        var eventId =
            eventData.GetProperty("id").GetString();

        var attributes =
            eventData.GetProperty("attributes");

        var eventType =
            attributes.GetProperty("type").GetString();

        if (string.IsNullOrWhiteSpace(eventId) ||
            string.IsNullOrWhiteSpace(eventType))
        {
            return BadRequest();
        }

        var lease = await new WebhookClaimService(_context).ClaimAsync(
            "paymongo", eventId, eventType, rawBody, cancellationToken);
        var webhookEvent = lease.Event;
        if (webhookEvent.Processed) return Ok();
        if (!lease.Claimed)
        {
            Response.Headers.RetryAfter = "30";
            return StatusCode(503);
        }

        try
        {
            if (eventType is
                "checkout_session.payment.paid" or
                "payment.paid")
            {
                await ProcessPaidEventAsync(
                    attributes,
                    rawBody,
                    cancellationToken);
            }

            webhookEvent.Processed = true;
            webhookEvent.ProcessingUntil = null;
            webhookEvent.ProcessedAt =
                DateTime.UtcNow;

            await _context.SaveChangesAsync(
                cancellationToken);

            return Ok();
        }
        catch (Exception exception)
        {
            var failedEventId = webhookEvent.Id;
            _context.ChangeTracker.Clear();
            webhookEvent = await _context.PaymentWebhookEvents.SingleAsync(e => e.Id == failedEventId, CancellationToken.None);

            webhookEvent.ProcessingError = "processing_failed";
            webhookEvent.ProcessingUntil = null;

            await _context.SaveChangesAsync(
                cancellationToken);

            _logger.LogError(
                exception,
                "PayMongo webhook processing failed.");

            return StatusCode(
                StatusCodes.Status500InternalServerError);
        }
    }

    private async Task ProcessPaidEventAsync(
        JsonElement eventAttributes,
        string rawBody,
        CancellationToken cancellationToken)
    {
        var resource =
            eventAttributes.GetProperty("data");

        var resourceId =
            resource.GetProperty("id").GetString();

        var resourceAttributes =
            resource.GetProperty("attributes");

        Guid orderId;

        if (!TryReadOrderId(
                resourceAttributes,
                out orderId))
        {
            throw new InvalidOperationException(
                "Alpha order ID was not found in PayMongo metadata.");
        }

        var payment =
            await _context.Payments
                .FirstOrDefaultAsync(
                    x => x.OrderId == orderId,
                    cancellationToken)
            ?? throw new InvalidOperationException(
                "Alpha payment was not found.");

        var checkoutSessionId = payment.GatewayCheckoutSessionId
            ?? throw new InvalidOperationException("No checkout session is bound to the payment.");

        // Retrieve the session directly from PayMongo.
        // Do not trust only the incoming webhook body.
        using var checkout =
            await _payMongo
                .RetrieveCheckoutSessionAsync(
                    checkoutSessionId,
                    cancellationToken);

        var checkoutAttributes =
            checkout.RootElement
                .GetProperty("data")
                .GetProperty("attributes");

        var status =
            checkoutAttributes.TryGetProperty(
                "payment_status",
                out var statusElement)
                ? statusElement.GetString()
                : null;

        if (!string.Equals(
                status,
                "paid",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"PayMongo session is not paid. Status: {status}");
        }

        if (!PayMongoCheckoutValidator.TryValidate(checkout.RootElement, checkoutSessionId,
            orderId, payment.Amount, payment.Currency, out var gatewayPaymentId))
            throw new InvalidOperationException("Provider payment did not match the expected checkout, order, amount and currency.");

        await _paymentCompletionService
            .CompleteOrderPaymentAsync(
                orderId:
                    orderId,
                gateway:
                    "paymongo",
                paymentMethod:
                    "paymongo_gcash",
                transactionReference:
                    checkoutSessionId,
                gatewayPaymentId:
                    gatewayPaymentId,
                rawGatewayResponse:
                    checkout.RootElement.GetRawText(),
                gatewayFee:
                    null,
                cancellationToken:
                    cancellationToken);
    }

    private static bool TryReadOrderId(
        JsonElement attributes,
        out Guid orderId)
    {
        orderId = Guid.Empty;

        if (!attributes.TryGetProperty(
                "metadata",
                out var metadata))
        {
            return false;
        }

        if (!metadata.TryGetProperty(
                "alpha_order_id",
                out var orderElement))
        {
            return false;
        }

        return Guid.TryParse(
            orderElement.GetString(),
            out orderId);
    }
}