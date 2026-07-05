using Alpha.API.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Alpha.API.Controllers;

[ApiController]
[Route("api/receipts")]
public class ReceiptsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ReceiptsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("order/{orderId}/html")]
    public async Task<IActionResult> GetOrderReceiptHtml(Guid orderId)
    {
        var order = await _context.Orders.FindAsync(orderId);
        if (order == null) return NotFound("Order not found.");

        var payment = await _context.Payments
            .FirstOrDefaultAsync(x => x.OrderId == orderId);

        var financial = await _context.OrderFinancials
            .FirstOrDefaultAsync(x => x.OrderId == orderId);

        if (payment == null || financial == null)
            return NotFound("Payment or financial record missing.");

        if (payment.PaymentStatus != "paid")
            return BadRequest("Receipt is only available after payment is completed.");

        var taxBreakdown = await _context.TaxCalculations
            .Where(x => x.OrderId == orderId)
            .OrderBy(x => x.Component)
            .ToListAsync();

        var taxRows = taxBreakdown.Any()
            ? string.Join("", taxBreakdown.Select(t => $@"
<div class='row'>
    <span>{WebUtility.HtmlEncode(ToLabel(t.Component))} {WebUtility.HtmlEncode(t.TaxType)} ({t.TaxRate * 100:0.##}%)</span>
    <span>{WebUtility.HtmlEncode(financial.Currency)} {t.TaxAmount:0.00}</span>
</div>"))
            : $@"
<div class='row'>
    <span>Tax</span>
    <span>{WebUtility.HtmlEncode(financial.Currency)} {financial.Tax:0.00}</span>
</div>";

        var html = $@"
<!doctype html>
<html>
<head>
<title>Receipt - {WebUtility.HtmlEncode(order.OrderNumber)}</title>
<style>
body {{ font-family: Arial; padding: 40px; color:#111827; }}
.card {{ border: 1px solid #ddd; padding: 24px; border-radius: 12px; max-width:720px; }}
.row {{ display:flex; justify-content:space-between; gap:20px; margin:8px 0; }}
.section-title {{ margin-top:22px; font-size:16px; border-top:1px solid #ddd; padding-top:14px; }}
.total {{ font-size:22px; font-weight:bold; border-top:1px solid #ddd; padding-top:12px; margin-top:14px; }}
.muted {{ color:#6b7280; font-size:13px; }}
@media print {{ button {{ display:none; }} }}
</style>
</head>
<body>
<button onclick='window.print()'>Download / Print PDF</button>

<div class='card'>
<h1>Alpha Auto Receipt</h1>

<p><strong>Order:</strong> {WebUtility.HtmlEncode(order.OrderNumber)}</p>
<p><strong>Customer:</strong> {WebUtility.HtmlEncode(order.CustomerName)}</p>
<p><strong>Payment Method:</strong> {WebUtility.HtmlEncode(payment.PaymentMethod)}</p>
<p><strong>Transaction:</strong> {WebUtility.HtmlEncode(payment.TransactionReference ?? "N/A")}</p>
<p><strong>Paid At:</strong> {payment.PaidAt?.ToString("yyyy-MM-dd HH:mm") ?? "N/A"} UTC</p>

<h3 class='section-title'>Charges</h3>
<div class='row'><span>Item Subtotal</span><span>{WebUtility.HtmlEncode(financial.Currency)} {financial.ItemSubtotal:0.00}</span></div>
<div class='row'><span>Delivery Fee</span><span>{WebUtility.HtmlEncode(financial.Currency)} {financial.DeliveryFee:0.00}</span></div>
<div class='row'><span>Service Fee</span><span>{WebUtility.HtmlEncode(financial.Currency)} {financial.ServiceFee:0.00}</span></div>
{RenderMechanicFee(financial.Currency, financial.MechanicAmount)}
{RenderDiscount(financial.Currency, financial.Discount)}

<h3 class='section-title'>Tax Breakdown</h3>
{taxRows}

<div class='row'><span>Total Tax</span><span>{WebUtility.HtmlEncode(financial.Currency)} {financial.Tax:0.00}</span></div>
<div class='row total'><span>Amount Paid</span><span>{WebUtility.HtmlEncode(financial.Currency)} {financial.TotalAmount:0.00}</span></div>
</div>
</body>
</html>";

        return Content(html, "text/html");
    }

    private static string ToLabel(string value)
    {
        return value switch
        {
            "product" => "Product",
            "delivery" => "Delivery",
            "alpha_service_fee" => "Alpha Service Fee",
            "mechanic" => "Mechanic Service",
            _ => value.Replace("_", " ")
        };
    }

    private static string RenderMechanicFee(string currency, decimal amount)
    {
        if (amount <= 0) return "";

        return $@"
<div class='row'>
    <span>Mechanic Fee</span>
    <span>{WebUtility.HtmlEncode(currency)} {amount:0.00}</span>
</div>";
    }

    private static string RenderDiscount(string currency, decimal amount)
    {
        if (amount <= 0) return "";

        return $@"
<div class='row'>
    <span>Discount</span>
    <span>-{WebUtility.HtmlEncode(currency)} {amount:0.00}</span>
</div>";
    }
}