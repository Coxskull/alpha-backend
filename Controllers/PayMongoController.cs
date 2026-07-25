using Alpha.API.Constants;
using Alpha.API.Data;
using Alpha.API.DTOs;
using Alpha.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Alpha.API.Controllers;

[ApiController]
[Route("api/paymongo")]
public class PayMongoController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly PayMongoService _payMongo;
    private readonly PaymentCompletionService
        _paymentCompletionService;

    public PayMongoController(
        AppDbContext context,
        PayMongoService payMongo,
        PaymentCompletionService paymentCompletionService)
    {
        _context = context;
        _payMongo = payMongo;
        _paymentCompletionService =
            paymentCompletionService;
    }

    [HttpPost("create-checkout")]
    public async Task<IActionResult> CreateCheckout(
        CreatePayMongoCheckoutDto dto,
        CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .FirstOrDefaultAsync(
                x => x.Id == dto.OrderId,
                cancellationToken);

        if (order == null)
        {
            return NotFound(new
            {
                message = "Order not found."
            });
        }

        if (order.Status != OrderStatuses.PaymentPending &&
            order.Status != "payment_pending")
        {
            return BadRequest(new
            {
                message =
                    $"Order is not awaiting payment. Current status: {order.Status}"
            });
        }

        if (order.CountryCode != "PH" ||
            order.Currency != "PHP")
        {
            return BadRequest(new
            {
                message =
                    "PayMongo GCash requires a Philippine PHP order."
            });
        }

        var financial = await _context.OrderFinancials
            .FirstOrDefaultAsync(
                x => x.OrderId == dto.OrderId,
                cancellationToken);

        var payment = await _context.Payments
            .FirstOrDefaultAsync(
                x => x.OrderId == dto.OrderId,
                cancellationToken);

        if (financial == null || payment == null)
        {
            return NotFound(new
            {
                message =
                    "Order financial or payment record was not found."
            });
        }

        if (payment.PaymentStatus == "paid")
        {
            return BadRequest(new
            {
                message = "This order is already paid."
            });
        }

        if (payment.PaymentMethod != "paymongo_gcash")
        {
            return BadRequest(new
            {
                message =
                    "This order was not created for PayMongo GCash."
            });
        }

        // Return the existing active checkout instead
        // of creating multiple sessions.
        if (!string.IsNullOrWhiteSpace(
                payment.CheckoutUrl) &&
            !string.IsNullOrWhiteSpace(
                payment.GatewayCheckoutSessionId))
        {
            return Ok(new
            {
                checkoutSessionId =
                    payment.GatewayCheckoutSessionId,
                checkoutUrl =
                    payment.CheckoutUrl,
                paymentStatus =
                    payment.PaymentStatus
            });
        }

        try
        {
            var result =
                await _payMongo
                    .CreateGcashCheckoutSessionAsync(
                        order,
                        financial,
                        payment,
                        cancellationToken);

            payment.PaymentGateway = "paymongo";
            payment.PaymentMethod =
                "paymongo_gcash";
            payment.PaymentStatus =
                "checkout_created";
            payment.TransactionReference =
                result.CheckoutSessionId;
            payment.GatewayCheckoutSessionId =
                result.CheckoutSessionId;
            payment.CheckoutUrl =
                result.CheckoutUrl;
            payment.GatewayResponse =
                JsonDocument.Parse(
                    result.RawResponse);

            await _context.SaveChangesAsync(
                cancellationToken);

            return Ok(new
            {
                orderId = order.Id,
                checkoutSessionId =
                    result.CheckoutSessionId,
                checkoutUrl =
                    result.CheckoutUrl,
                currency =
                    financial.Currency,
                amount =
                    financial.TotalAmount,
                paymentStatus =
                    payment.PaymentStatus
            });
        }
        catch (Exception exception)
        {
            payment.PaymentStatus =
                "checkout_failed";
            payment.FailureReason =
                exception.Message;

            await _context.SaveChangesAsync(
                cancellationToken);

            return BadRequest(new
            {
                message =
                    "Unable to create PayMongo checkout.",
                error =
                    exception.Message
            });
        }
    }

    [HttpPost("verify")]
    public async Task<IActionResult> VerifyCheckout(
        VerifyPayMongoCheckoutDto dto,
        CancellationToken cancellationToken)
    {
        var payment = await _context.Payments
            .FirstOrDefaultAsync(
                x => x.OrderId == dto.OrderId,
                cancellationToken);

        if (payment == null)
        {
            return NotFound(new
            {
                message = "Payment not found."
            });
        }

        if (!string.Equals(
                payment.GatewayCheckoutSessionId,
                dto.CheckoutSessionId,
                StringComparison.Ordinal))
        {
            return BadRequest(new
            {
                message =
                    "Checkout session does not match the order."
            });
        }

        var checkout =
            await _payMongo
                .RetrieveCheckoutSessionAsync(
                    dto.CheckoutSessionId,
                    cancellationToken);

        var rawJson =
            checkout.RootElement.GetRawText();

        var attributes =
            checkout.RootElement
                .GetProperty("data")
                .GetProperty("attributes");

        var paymentStatus =
            attributes.TryGetProperty(
                "payment_status",
                out var statusElement)
                ? statusElement.GetString()
                : null;

        if (!string.Equals(
                paymentStatus,
                "paid",
                StringComparison.OrdinalIgnoreCase))
        {
            return Ok(new
            {
                paid = false,
                paymentStatus =
                    paymentStatus ?? "pending"
            });
        }

        string? payMongoPaymentId = null;

        if (attributes.TryGetProperty(
                "payments",
                out var paymentsElement) &&
            paymentsElement.ValueKind ==
                JsonValueKind.Array &&
            paymentsElement.GetArrayLength() > 0)
        {
            payMongoPaymentId =
                paymentsElement[0]
                    .GetProperty("id")
                    .GetString();
        }

        await _paymentCompletionService
            .CompleteOrderPaymentAsync(
                orderId:
                    dto.OrderId,
                gateway:
                    "paymongo",
                paymentMethod:
                    "paymongo_gcash",
                transactionReference:
                    dto.CheckoutSessionId,
                gatewayPaymentId:
                    payMongoPaymentId,
                rawGatewayResponse:
                    rawJson,
                gatewayFee:
                    null,
                cancellationToken:
                    cancellationToken);

        return Ok(new
        {
            paid = true,
            paymentStatus = "paid",
            orderId = dto.OrderId
        });
    }
}