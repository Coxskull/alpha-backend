using Alpha.API.Constants;
using Alpha.API.Data;
using Alpha.API.DTOs;
using Alpha.API.Models;
using Alpha.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Alpha.API.Controllers;

[ApiController]
[Route("api/paypal")]
public class PayPalController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly PayPalService _paypal;
    private readonly SettlementService _settlements;
    private readonly ReferralCommissionService _referralCommissionService;

    public PayPalController(
        AppDbContext context,
        PayPalService payPalService,
        SettlementService settlementService,
        ReferralCommissionService referralCommissionService)
    {
        _context = context;
        _paypal = payPalService;
        _settlements = settlementService;
        _referralCommissionService = referralCommissionService;
    }

    // =====================================================
    // CREATE PAYPAL ORDER
    // =====================================================

    [HttpPost("create-order")]
    public async Task<IActionResult> CreatePayPalOrder(
        CreatePayPalOrderDto dto,
        CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .FirstOrDefaultAsync(
                x => x.Id == dto.OrderId,
                cancellationToken
            );

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
                    $"Order is not waiting for payment. Current status: {order.Status}"
            });
        }

        var financial = await _context.OrderFinancials
            .FirstOrDefaultAsync(
                x => x.OrderId == dto.OrderId,
                cancellationToken
            );

        if (financial == null)
        {
            return NotFound(new
            {
                message = "Financial record not found."
            });
        }

        var payment = await _context.Payments
            .FirstOrDefaultAsync(
                x => x.OrderId == dto.OrderId,
                cancellationToken
            );

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
                message = "This order is already paid."
            });
        }

        try
        {
            var paypalOrderId = await _paypal.CreateOrder(
                order,
                financial
            );

            payment.PaymentMethod = "paypal";
            payment.PaymentStatus = "paypal_created";
            payment.TransactionReference = paypalOrderId;

            await _context.SaveChangesAsync(cancellationToken);

            return Ok(new
            {
                id = paypalOrderId,
                orderId = order.Id,
                paymentStatus = payment.PaymentStatus
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                message = "Unable to create PayPal order.",
                error = ex.Message
            });
        }
    }

    // =====================================================
    // CAPTURE PAYPAL ORDER
    // =====================================================

    [HttpPost("capture-order")]
    public async Task<IActionResult> CapturePayPalOrder(
        CapturePayPalOrderDto dto,
        CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .FirstOrDefaultAsync(
                x => x.Id == dto.OrderId,
                cancellationToken
            );

        if (order == null)
        {
            return NotFound(new
            {
                message = "Order not found."
            });
        }

        var payment = await _context.Payments
            .FirstOrDefaultAsync(
                x => x.OrderId == dto.OrderId,
                cancellationToken
            );

        if (payment == null)
        {
            return NotFound(new
            {
                message = "Payment record not found."
            });
        }

        if (payment.PaymentStatus == "paid")
        {
            return Ok(new
            {
                message = "Payment already captured.",
                orderStatus = order.Status,
                paymentStatus = payment.PaymentStatus,
                transactionReference =
                    payment.TransactionReference
            });
        }

        if (string.IsNullOrWhiteSpace(
                payment.TransactionReference))
        {
            return BadRequest(new
            {
                message =
                    "No PayPal order reference was found for this payment."
            });
        }

        if (!string.Equals(
                payment.TransactionReference,
                dto.PayPalOrderId,
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message =
                    "PayPal order does not match this Alpha order."
            });
        }

        try
        {
            var capture = await _paypal.CaptureOrder(
                dto.PayPalOrderId
            );

            var root = capture.RootElement;

            if (!root.TryGetProperty(
                    "status",
                    out var statusElement))
            {
                return BadRequest(new
                {
                    message =
                        "PayPal did not return a valid payment status."
                });
            }

            var status = statusElement.GetString();

            if (!string.Equals(
                    status,
                    "COMPLETED",
                    StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    message =
                        $"PayPal payment was not completed. Status: {status}"
                });
            }

            string? captureId = null;

            try
            {
                captureId = root
                    .GetProperty("purchase_units")[0]
                    .GetProperty("payments")
                    .GetProperty("captures")[0]
                    .GetProperty("id")
                    .GetString();
            }
            catch
            {
                // Use the PayPal order ID as fallback when the
                // nested capture ID is not available.
                captureId = dto.PayPalOrderId;
            }

            var financial = await _context.OrderFinancials
                .FirstOrDefaultAsync(
                    x => x.OrderId == dto.OrderId,
                    cancellationToken
                );

            if (financial == null)
            {
                return NotFound(new
                {
                    message = "Financial record not found."
                });
            }

            /*
             * The orders table should contain customer_id.
             *
             * Order.cs:
             * public Guid? CustomerId { get; set; }
             */
            if (!order.CustomerId.HasValue)
            {
                return BadRequest(new
                {
                    message =
                        "The order is not connected to a registered customer. " +
                        "Set orders.customer_id when creating the order."
                });
            }

            var customerUserId = order.CustomerId.Value;
            var now = DateTime.UtcNow;

            await using var databaseTransaction =
                await _context.Database.BeginTransactionAsync(
                    cancellationToken
                );

            try
            {
                payment.PaymentStatus = "paid";
                payment.PaymentMethod = "paypal";
                payment.TransactionReference =
                    captureId ?? dto.PayPalOrderId;
                payment.PaidAt = now;

                financial.CustomerPaid =
                    financial.TotalAmount;

                financial.FinancialStatus =
                    "paid_pending_dispatch";

                financial.PayoutStatus = "not_ready";

                order.Status =
                    OrderStatuses.WaitingForSupplier;

                order.UpdatedAt = now;

                _context.StatusHistory.Add(
                    new StatusHistory
                    {
                        Id = Guid.NewGuid(),
                        OrderId = order.Id,
                        Status = OrderStatuses.PaymentPaid,
                        Notes = "PayPal payment captured.",
                        CreatedAt = now
                    }
                );

                _context.StatusHistory.Add(
                    new StatusHistory
                    {
                        Id = Guid.NewGuid(),
                        OrderId = order.Id,
                        Status =
                            OrderStatuses.WaitingForSupplier,
                        Notes =
                            "Order moved to the supplier assignment queue.",
                        CreatedAt = now
                    }
                );

                _context.AuditLogs.Add(
                    new AuditLog
                    {
                        Id = Guid.NewGuid(),
                        OrderId = order.Id,
                        Action =
                            "PayPal Payment Captured",
                        PerformedBy = "paypal",
                        CreatedAt = now
                    }
                );

                await _context.SaveChangesAsync(
                    cancellationToken
                );

                await _settlements
                    .CreateOrUpdateSettlementAfterPayment(
                        order.Id
                    );

                await _referralCommissionService
                    .GenerateOrderCommissionAsync(
                        sourceUserId: customerUserId,
                        orderId: order.Id,
                        paymentId: payment.Id,
                        grossAmount: payment.Amount,
                        currency: payment.Currency,
                        transactionType: "customer_order",
                        description:
                            $"Completed payment for order {order.OrderNumber}",
                        cancellationToken:
                            cancellationToken
                    );

                await databaseTransaction.CommitAsync(
                    cancellationToken
                );
            }
            catch
            {
                await databaseTransaction.RollbackAsync(
                    cancellationToken
                );

                throw;
            }

            return Ok(new
            {
                message =
                    "Payment captured. Order is ready for dispatch.",
                orderId = order.Id,
                orderNumber = order.OrderNumber,
                orderStatus = order.Status,
                paymentStatus = payment.PaymentStatus,
                captureId
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                message =
                    "PayPal payment capture failed.",
                error = ex.Message
            });
        }
    }

    // =====================================================
    // REFUND PAYPAL PAYMENT
    // =====================================================

    [HttpPost("refund")]
    public async Task<IActionResult> Refund(
        RefundPaymentDto dto,
        CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .FirstOrDefaultAsync(
                x => x.Id == dto.OrderId,
                cancellationToken
            );

        if (order == null)
        {
            return NotFound(new
            {
                message = "Order not found."
            });
        }

        var payment = await _context.Payments
            .FirstOrDefaultAsync(
                x => x.OrderId == dto.OrderId,
                cancellationToken
            );

        if (payment == null)
        {
            return NotFound(new
            {
                message = "Payment not found."
            });
        }

        if (payment.PaymentStatus != "paid")
        {
            return BadRequest(new
            {
                message =
                    "Only paid payments can be refunded."
            });
        }

        if (string.IsNullOrWhiteSpace(
                payment.TransactionReference))
        {
            return BadRequest(new
            {
                message =
                    "The PayPal capture reference is missing."
            });
        }

        var remainingRefundableAmount =
            payment.Amount - payment.RefundedAmount;

        if (dto.Amount <= 0)
        {
            return BadRequest(new
            {
                message =
                    "The refund amount must be greater than zero."
            });
        }

        if (dto.Amount > remainingRefundableAmount)
        {
            return BadRequest(new
            {
                message =
                    $"The maximum refundable amount is {remainingRefundableAmount} {payment.Currency}."
            });
        }

        try
        {
            var refund = await _paypal.RefundCapture(
                payment.TransactionReference,
                dto.Amount,
                payment.Currency
            );

            var refundRoot = refund.RootElement;

            string? refundId = null;

            if (refundRoot.TryGetProperty(
                    "id",
                    out var refundIdElement))
            {
                refundId =
                    refundIdElement.GetString();
            }

            var now = DateTime.UtcNow;

            await using var databaseTransaction =
                await _context.Database.BeginTransactionAsync(
                    cancellationToken
                );

            try
            {
                payment.RefundedAmount += dto.Amount;

                var isFullyRefunded =
                    payment.RefundedAmount >=
                    payment.Amount;

                payment.RefundStatus =
                    isFullyRefunded
                        ? "fully_refunded"
                        : "partially_refunded";

                payment.RefundReference = refundId;
                payment.RefundedAt = now;

                if (isFullyRefunded)
                {
                    payment.PaymentStatus = "refunded";
                    order.Status = "refunded";
                    order.UpdatedAt = now;
                }

                _context.StatusHistory.Add(
                    new StatusHistory
                    {
                        Id = Guid.NewGuid(),
                        OrderId = order.Id,
                        Status = isFullyRefunded
                            ? "fully_refunded"
                            : "partially_refunded",
                        Notes =
                            $"PayPal refund processed: {dto.Amount} {payment.Currency}.",
                        CreatedAt = now
                    }
                );

                _context.AuditLogs.Add(
                    new AuditLog
                    {
                        Id = Guid.NewGuid(),
                        OrderId = order.Id,
                        Action = isFullyRefunded
                            ? "PayPal Payment Fully Refunded"
                            : "PayPal Payment Partially Refunded",
                        PerformedBy = "paypal",
                        CreatedAt = now
                    }
                );

                await _context.SaveChangesAsync(
                    cancellationToken
                );

                /*
                 * This reverses the referral commissions
                 * generated from the original order.
                 *
                 * For partial refunds, your referral service
                 * should either prorate the reversal or avoid
                 * reversing everything until fully refunded.
                 */
                if (isFullyRefunded)
                {
                    await _referralCommissionService
                        .ReverseOrderCommissionsAsync(
                            order.Id,
                            "Customer payment was fully refunded.",
                            cancellationToken
                        );
                }

                await databaseTransaction.CommitAsync(
                    cancellationToken
                );
            }
            catch
            {
                await databaseTransaction.RollbackAsync(
                    cancellationToken
                );

                throw;
            }

            return Ok(new
            {
                message = payment.RefundStatus ==
                          "fully_refunded"
                    ? "Full refund completed."
                    : "Partial refund completed.",
                refundId,
                refundedAmount =
                    payment.RefundedAmount,
                remainingRefundableAmount =
                    payment.Amount -
                    payment.RefundedAmount,
                refundStatus =
                    payment.RefundStatus,
                paymentStatus =
                    payment.PaymentStatus,
                orderStatus = order.Status
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                message =
                    "PayPal refund failed.",
                error = ex.Message
            });
        }
    }

    // =====================================================
    // VERIFY SETTLEMENT
    // =====================================================

    [HttpPost("orders/{orderId:guid}/verify-settlement")]
    [Authorize(Roles = "admin,dispatcher")]
    public async Task<IActionResult> VerifySettlement(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        try
        {
            var orderExists =
                await _context.Orders.AnyAsync(
                    x => x.Id == orderId,
                    cancellationToken
                );

            if (!orderExists)
            {
                return NotFound(new
                {
                    message = "Order not found."
                });
            }

            var result =
                await _settlements.VerifySettlement(
                    orderId
                );

            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                message =
                    "Settlement verification failed.",
                error = ex.Message
            });
        }
    }
}
