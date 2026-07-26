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
    private readonly PaymentCompletionService _paymentCompletionService;

    public PayMongoController(
        AppDbContext context,
        PayMongoService payMongo,
        PaymentCompletionService paymentCompletionService)
    {
        _context = context;
        _payMongo = payMongo;
        _paymentCompletionService = paymentCompletionService;
    }

    // =====================================================
    // CREATE PAYMONGO GCASH CHECKOUT SESSION
    // =====================================================

    [HttpPost("create-checkout")]
    public async Task<IActionResult> CreateCheckout(
        CreatePayMongoCheckoutDto dto,
        CancellationToken cancellationToken)
    {
        if (dto.OrderId == Guid.Empty)
        {
            return BadRequest(new
            {
                message = "A valid order ID is required."
            });
        }

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

        if (!string.Equals(
                order.Status,
                OrderStatuses.PaymentPending,
                StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(
                order.Status,
                "payment_pending",
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message =
                    $"Order is not awaiting payment. Current status: {order.Status}"
            });
        }

        if (!string.Equals(
                order.CountryCode,
                "PH",
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                order.Currency,
                "PHP",
                StringComparison.OrdinalIgnoreCase))
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

        if (financial == null)
        {
            return NotFound(new
            {
                message = "Order financial record was not found."
            });
        }

        var payment = await _context.Payments
            .FirstOrDefaultAsync(
                x => x.OrderId == dto.OrderId,
                cancellationToken);

        if (payment == null)
        {
            return NotFound(new
            {
                message = "Payment record was not found."
            });
        }

        if (string.Equals(
                payment.PaymentStatus,
                "paid",
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message = "This order is already paid."
            });
        }

        if (!string.Equals(
                payment.PaymentMethod,
                "paymongo_gcash",
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message =
                    "This order was not created for PayMongo GCash."
            });
        }

        if (payment.Amount != financial.TotalAmount)
        {
            return BadRequest(new
            {
                message =
                    "Payment amount does not match the order financial total."
            });
        }

        if (!string.Equals(
                payment.Currency,
                financial.Currency,
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message =
                    "Payment currency does not match the financial currency."
            });
        }

        /*
         * Return the existing checkout session instead of creating
         * multiple PayMongo checkout sessions for the same order.
         */
        if (!string.IsNullOrWhiteSpace(payment.CheckoutUrl) &&
            !string.IsNullOrWhiteSpace(
                payment.GatewayCheckoutSessionId))
        {
            return Ok(new
            {
                orderId = order.Id,
                checkoutSessionId =
                    payment.GatewayCheckoutSessionId,
                checkoutUrl =
                    payment.CheckoutUrl,
                currency =
                    payment.Currency,
                amount =
                    payment.Amount,
                paymentStatus =
                    payment.PaymentStatus
            });
        }

        try
        {
            var result = await _payMongo
                .CreateGcashCheckoutSessionAsync(
                    order,
                    financial,
                    payment,
                    cancellationToken);

            if (string.IsNullOrWhiteSpace(
                    result.CheckoutSessionId))
            {
                throw new InvalidOperationException(
                    "PayMongo did not return a checkout session ID.");
            }

            if (string.IsNullOrWhiteSpace(
                    result.CheckoutUrl))
            {
                throw new InvalidOperationException(
                    "PayMongo did not return a checkout URL.");
            }

            /*
             * The previous code referenced checkoutSessionResponse,
             * but that variable did not exist.
             *
             * Serialize the actual result returned by PayMongoService.
             */
            var checkoutSessionResponse =
                JsonSerializer.Serialize(
                    result,
                    new JsonSerializerOptions
                    {
                        PropertyNamingPolicy =
                            JsonNamingPolicy.CamelCase,
                        WriteIndented = false
                    });

            payment.PaymentGateway = "paymongo";
            payment.PaymentMethod = "paymongo_gcash";
            payment.PaymentStatus = "checkout_created";

            payment.TransactionReference =
                result.CheckoutSessionId;

            payment.GatewayCheckoutSessionId =
                result.CheckoutSessionId;

            payment.CheckoutUrl =
                result.CheckoutUrl;

            payment.GatewayResponse =
    JsonDocument.Parse(
        JsonSerializer.Serialize(result));

            payment.FailureReason = null;

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
        catch (OperationCanceledException)
        {
            throw;
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

    // =====================================================
    // VERIFY PAYMONGO CHECKOUT SESSION
    // =====================================================

    [HttpPost("verify")]
    public async Task<IActionResult> VerifyCheckout(
        VerifyPayMongoCheckoutDto dto,
        CancellationToken cancellationToken)
    {
        if (dto.OrderId == Guid.Empty)
        {
            return BadRequest(new
            {
                message = "A valid order ID is required."
            });
        }

        if (string.IsNullOrWhiteSpace(
                dto.CheckoutSessionId))
        {
            return BadRequest(new
            {
                message =
                    "Checkout session ID is required."
            });
        }

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

        /*
         * This makes the endpoint safe when the frontend calls
         * verification more than once after a successful payment.
         */
        if (string.Equals(
                payment.PaymentStatus,
                "paid",
                StringComparison.OrdinalIgnoreCase))
        {
            return Ok(new
            {
                paid = true,
                paymentStatus =
                    payment.PaymentStatus,
                orderId =
                    dto.OrderId,
                gatewayPaymentId =
                    payment.GatewayPaymentId
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

        try
        {
            using var checkout = await _payMongo
                .RetrieveCheckoutSessionAsync(
                    dto.CheckoutSessionId,
                    cancellationToken);

            var rawJson =
                checkout.RootElement.GetRawText();

            if (!checkout.RootElement.TryGetProperty(
                    "data",
                    out var dataElement))
            {
                return BadRequest(new
                {
                    message =
                        "PayMongo returned an invalid checkout response."
                });
            }

            if (!dataElement.TryGetProperty(
                    "attributes",
                    out var attributes))
            {
                return BadRequest(new
                {
                    message =
                        "PayMongo checkout attributes were not found."
                });
            }

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
                /*
                 * Store the latest gateway response even when
                 * the checkout is still pending or has failed.
                 */
                payment.GatewayResponse =
                    rawJson;

                payment.PaymentStatus =
                    string.IsNullOrWhiteSpace(paymentStatus)
                        ? "pending"
                        : paymentStatus.Trim().ToLowerInvariant();

                await _context.SaveChangesAsync(
                    cancellationToken);

                return Ok(new
                {
                    paid = false,
                    paymentStatus =
                        paymentStatus ?? "pending",
                    orderId =
                        dto.OrderId
                });
            }

            string? payMongoPaymentId =
                ExtractPayMongoPaymentId(
                    attributes);

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
                orderId = dto.OrderId,
                checkoutSessionId =
                    dto.CheckoutSessionId,
                gatewayPaymentId =
                    payMongoPaymentId
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            payment.FailureReason =
                exception.Message;

            await _context.SaveChangesAsync(
                cancellationToken);

            return BadRequest(new
            {
                message =
                    "Unable to verify PayMongo checkout.",
                error =
                    exception.Message
            });
        }
    }

    // =====================================================
    // HELPER: EXTRACT PAYMONGO PAYMENT ID
    // =====================================================

    private static string? ExtractPayMongoPaymentId(
        JsonElement attributes)
    {
        if (!attributes.TryGetProperty(
                "payments",
                out var paymentsElement))
        {
            return null;
        }

        if (paymentsElement.ValueKind !=
            JsonValueKind.Array)
        {
            return null;
        }

        if (paymentsElement.GetArrayLength() == 0)
        {
            return null;
        }

        var firstPayment =
            paymentsElement[0];

        if (firstPayment.ValueKind !=
            JsonValueKind.Object)
        {
            return null;
        }

        if (!firstPayment.TryGetProperty(
                "id",
                out var paymentIdElement))
        {
            return null;
        }

        return paymentIdElement.GetString();
    }

    [HttpGet("orders/{orderId:guid}/payment-status")]
    public async Task<IActionResult> GetPaymentStatus(
    Guid orderId,
    CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == orderId,
                cancellationToken);

        if (order == null)
        {
            return NotFound(new
            {
                message = "Order not found."
            });
        }

        var payment = await _context.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.OrderId == orderId,
                cancellationToken);

        if (payment == null)
        {
            return NotFound(new
            {
                message = "Payment record not found."
            });
        }

        var normalizedStatus =
            payment.PaymentStatus?
                .Trim()
                .ToLowerInvariant()
            ?? "pending";

        return Ok(new
        {
            orderId = order.Id,
            orderStatus = order.Status,
            paymentStatus =
                payment.PaymentStatus,
            paymentGateway =
                payment.PaymentGateway,
            checkoutSessionId =
                payment.GatewayCheckoutSessionId,
            gatewayPaymentId =
                payment.GatewayPaymentId,
            isPaid =
                normalizedStatus == "paid" ||
                normalizedStatus == "completed" ||
                normalizedStatus == "succeeded",
            message =
                normalizedStatus == "paid"
                    ? "Payment confirmed."
                    : "Payment is still pending."
        });
    }
}