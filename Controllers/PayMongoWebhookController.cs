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
[Route("api/webhooks/paymongo")]
public class PayMongoWebhookController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly PayMongoService _payMongo;
    private readonly PaymentCompletionService
        _paymentCompletionService;
    private readonly ILogger<PayMongoWebhookController>
        _logger;

    public PayMongoWebhookController(
        AppDbContext context,
        PayMongoService payMongo,
        PaymentCompletionService paymentCompletionService,
        ILogger<PayMongoWebhookController> logger)
    {
        _context = context;
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

        var alreadyProcessed =
            await _context.PaymentWebhookEvents
                .AnyAsync(
                    x =>
                        x.Gateway == "paymongo" &&
                        x.GatewayEventId == eventId,
                    cancellationToken);

        if (alreadyProcessed)
            return Ok();

        var webhookEvent =
            new PaymentWebhookEvent
            {
                Id = Guid.NewGuid(),
                Gateway = "paymongo",
                GatewayEventId = eventId,
                EventType = eventType,
                Payload = JsonDocument.Parse(rawBody),
                Processed = false,
                ReceivedAt = DateTime.UtcNow
            };

        _context.PaymentWebhookEvents.Add(
            webhookEvent);

        await _context.SaveChangesAsync(
            cancellationToken);

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
            webhookEvent.ProcessedAt =
                DateTime.UtcNow;

            await _context.SaveChangesAsync(
                cancellationToken);

            return Ok();
        }
        catch (Exception exception)
        {
            webhookEvent.ProcessingError =
                exception.Message;

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

        var checkoutSessionId =
            payment.GatewayCheckoutSessionId
            ?? resourceId
            ?? throw new InvalidOperationException(
                "Checkout session ID was not found.");

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

        string? gatewayPaymentId = null;

        if (checkoutAttributes.TryGetProperty(
                "payments",
                out var payments) &&
            payments.ValueKind ==
                JsonValueKind.Array &&
            payments.GetArrayLength() > 0)
        {
            gatewayPaymentId =
                payments[0]
                    .GetProperty("id")
                    .GetString();
        }

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