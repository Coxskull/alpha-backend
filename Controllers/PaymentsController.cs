using Alpha.API.Data;
using Alpha.API.Models;
using Alpha.API.Models.Payments;
using Alpha.API.Services.Providers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Alpha.API.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly PaymentProviderFactory _factory;

    public PaymentsController(
        AppDbContext context,
        PaymentProviderFactory factory)
    {
        _context = context;
        _factory = factory;
    }

    [HttpPost("create")]
    public async Task<IActionResult> CreatePayment(
        CreatePaymentDto dto,
        CancellationToken cancellationToken)
    {
        if (dto.OrderId == Guid.Empty)
        {
            return BadRequest(new
            {
                message = "Order ID is required."
            });
        }

        var order =
            await _context.Orders
                .Include(x => x.CustomerUser)
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

        var financial =
            await _context.OrderFinancials
                .FirstOrDefaultAsync(
                    x => x.OrderId == dto.OrderId,
                    cancellationToken);

        if (financial == null)
        {
            return NotFound(new
            {
                message = "Order financial record not found."
            });
        }

        var payment =
            await _context.Payments
                .FirstOrDefaultAsync(
                    x => x.OrderId == dto.OrderId,
                    cancellationToken);

        if (payment == null)
        {
            return NotFound(new
            {
                message = "Payment record not found."
            });
        }

        if (payment.PaymentStatus == "paid")
        {
            return BadRequest(new
            {
                message = "This order has already been paid."
            });
        }

        var gateway =
    dto.Gateway
        .Trim()
        .ToLowerInvariant();

        gateway = gateway switch
        {
            "paymongo_gcash" => "paymongo",
            "paypal" => "paypal",
            "stripe" => "stripe",
            "maya" => "maya",
            "xendit" => "xendit",
            "hitpay" => "hitpay",
            _ => gateway
        };



      
        var amount =
            financial.TotalAmount;

        var currency =
            financial.Currency
                .Trim()
                .ToLowerInvariant();

        var request =
            new PaymentRequest
            {
                OrderId = order.Id,

                Amount = amount,

                Currency = currency,

                CustomerEmail =
                    order.CustomerUser?.Email
                    ?? "",

                CustomerName =
                    order.CustomerName,

                Gateway = gateway,

                SuccessUrl = "",

                CancelUrl = ""
            };

        try
        {
            var provider =
                _factory.Get(gateway);

            var result =
                await provider.CreatePaymentAsync(
                    request);

            if (!result.Success)
            {
                payment.PaymentStatus =
                    "failed";

                payment.FailureReason =
                    result.Error;

                await _context.SaveChangesAsync(
                    cancellationToken);

                return BadRequest(new
                {
                    success = false,
                    error = result.Error
                });
            }

            payment.PaymentGateway =
                gateway;

            payment.PaymentMethod =
                gateway;

            payment.PaymentStatus =
                "pending";

            payment.GatewayCheckoutSessionId =
                result.PaymentId;

            payment.TransactionReference =
                result.TransactionReference;

            payment.CheckoutUrl =
                result.CheckoutUrl;

            payment.FailureReason = null;

            await _context.SaveChangesAsync(
                cancellationToken);

            return Ok(new
            {
                success = true,

                checkoutUrl =
                    result.CheckoutUrl,

                gatewayPaymentId =
                    result.PaymentId
            });
        }
        catch (Exception ex)
        {
            payment.PaymentStatus =
                "failed";

            payment.FailureReason =
                ex.Message;

            await _context.SaveChangesAsync(
                cancellationToken);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    success = false,
                    error =
                        "Unable to create Stripe checkout.",
                    detail = ex.Message
                });
        }
    }
}

public class CreatePaymentDto
{
    public Guid OrderId { get; set; }

    public string Gateway { get; set; }
        = string.Empty;
}