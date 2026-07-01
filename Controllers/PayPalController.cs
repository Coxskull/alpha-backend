// Controllers/PayPalController.cs
using Alpha.API.Data;
using Alpha.API.DTOs;
using Alpha.API.Models;
using Alpha.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace Alpha.API.Controllers;

[ApiController]
[Route("api/paypal")]
public class PayPalController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly PayPalService _paypal;

    public PayPalController(AppDbContext context, PayPalService paypal)
    {
        _context = context;
        _paypal = paypal;
    }

    [HttpPost("create-order")]
    public async Task<IActionResult> CreatePayPalOrder(CreatePayPalOrderDto dto)
    {
        var order = await _context.Orders.FindAsync(dto.OrderId);
        if (order == null) return NotFound("Order not found.");

        if (order.Status != "payment_pending")
            return BadRequest($"Order is not waiting for payment. Current status: {order.Status}");

        var financial = await _context.OrderFinancials
            .FirstOrDefaultAsync(x => x.OrderId == dto.OrderId);

        if (financial == null) return NotFound("Financial record not found.");

        var payment = await _context.Payments
            .FirstOrDefaultAsync(x => x.OrderId == dto.OrderId);

        if (payment == null) return NotFound("Payment record not found.");

        if (payment.PaymentStatus == "paid")
            return BadRequest("This order is already paid.");

        var paypalOrderId = await _paypal.CreateOrder(order, financial);

        payment.PaymentMethod = "paypal";
        payment.PaymentStatus = "paypal_created";
        payment.TransactionReference = paypalOrderId;

        await _context.SaveChangesAsync();

        return Ok(new { id = paypalOrderId });
    }

    [HttpPost("capture-order")]
    public async Task<IActionResult> CapturePayPalOrder(CapturePayPalOrderDto dto)
    {
        var order = await _context.Orders.FindAsync(dto.OrderId);
        if (order == null) return NotFound("Order not found.");

        var payment = await _context.Payments
            .FirstOrDefaultAsync(x => x.OrderId == dto.OrderId);

        if (payment == null) return NotFound("Payment record not found.");

        if (payment.PaymentStatus == "paid")
        {
            return Ok(new
            {
                message = "Payment already captured.",
                orderStatus = order.Status,
                paymentStatus = payment.PaymentStatus
            });
        }

        if (payment.TransactionReference != dto.PayPalOrderId)
            return BadRequest("PayPal order does not match this Alpha order.");

        var capture = await _paypal.CaptureOrder(dto.PayPalOrderId);
        var root = capture.RootElement;
        var status = root.GetProperty("status").GetString();

        if (status != "COMPLETED")
            return BadRequest($"PayPal payment not completed. Status: {status}");

        var captureId = root
            .GetProperty("purchase_units")[0]
            .GetProperty("payments")
            .GetProperty("captures")[0]
            .GetProperty("id")
            .GetString();

        var financial = await _context.OrderFinancials
            .FirstOrDefaultAsync(x => x.OrderId == dto.OrderId);

        if (financial == null) return NotFound("Financial record not found.");

        payment.PaymentStatus = "paid";
        payment.PaymentMethod = "paypal";
        payment.TransactionReference = captureId ?? dto.PayPalOrderId;
        payment.PaidAt = DateTime.UtcNow;

        financial.CustomerPaid = financial.TotalAmount;
        financial.FinancialStatus = "paid_pending_dispatch";
        financial.PayoutStatus = "not_ready";

        order.Status = "pending";
        order.UpdatedAt = DateTime.UtcNow;

        _context.StatusHistory.Add(new StatusHistory
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            Status = "payment_paid",
            Notes = "PayPal payment captured.",
            CreatedAt = DateTime.UtcNow
        });

        _context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            Action = "PayPal Payment Captured",
            PerformedBy = "paypal",
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Payment captured. Order ready for dispatch.",
            orderStatus = order.Status,
            paymentStatus = payment.PaymentStatus,
            captureId
        });
    }

    [HttpPost("refund")]
    public async Task<IActionResult> Refund(RefundPaymentDto dto)
    {
        var payment = await _context.Payments
            .FirstOrDefaultAsync(x => x.OrderId == dto.OrderId);

        if (payment == null) return NotFound("Payment not found.");

        if (payment.PaymentStatus != "paid")
            return BadRequest("Only paid payments can be refunded.");

        if (string.IsNullOrWhiteSpace(payment.TransactionReference))
            return BadRequest("Missing PayPal capture reference.");

        if (dto.Amount <= 0 || dto.Amount > payment.Amount)
            return BadRequest("Invalid refund amount.");

        var refund = await _paypal.RefundCapture(
            payment.TransactionReference,
            dto.Amount,
            payment.Currency
        );

        var refundId = refund.RootElement.GetProperty("id").GetString();

        payment.RefundedAmount += dto.Amount;
        payment.RefundStatus =
            payment.RefundedAmount >= payment.Amount ? "fully_refunded" : "partially_refunded";
        payment.RefundReference = refundId;
        payment.RefundedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Refund completed.",
            refundId,
            payment
        });
    }
}