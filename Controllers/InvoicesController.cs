using Alpha.API.Data;
using Alpha.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Alpha.API.Controllers;

[ApiController]
[Route("api/invoices")]
public class InvoicesController : ControllerBase
{
    private readonly AppDbContext _context;

    public InvoicesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost("order/{orderId}/generate")]
    public async Task<IActionResult> GenerateOrderInvoice(Guid orderId)
    {
        var order = await _context.Orders.FindAsync(orderId);
        if (order == null) return NotFound("Order not found.");

        var financial = await _context.OrderFinancials
            .FirstOrDefaultAsync(x => x.OrderId == orderId);

        if (financial == null) return NotFound("Financial record not found.");

        var existing = await _context.Invoices
            .FirstOrDefaultAsync(x => x.OrderId == orderId);

        if (existing != null) return Ok(existing);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMddHHmmss}",
            Subtotal = financial.ItemSubtotal
                + financial.DeliveryFee
                + financial.ServiceFee
                + financial.MechanicAmount,
            Tax = financial.Tax,
            Total = financial.TotalAmount,
            Currency = financial.Currency,
            IssuedAt = DateTime.UtcNow
        };

        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();

        return Ok(invoice);
    }

    [HttpGet("order/{orderId}/html")]
    public async Task<IActionResult> GetOrderInvoiceHtml(Guid orderId)
    {
        var order = await _context.Orders.FindAsync(orderId);
        if (order == null) return NotFound("Order not found.");

        var financial = await _context.OrderFinancials
            .FirstOrDefaultAsync(x => x.OrderId == orderId);

        if (financial == null) return NotFound("Financial record not found.");

        var invoice = await _context.Invoices
            .FirstOrDefaultAsync(x => x.OrderId == orderId);

        if (invoice == null)
        {
            invoice = new Invoice
            {
                Id = Guid.NewGuid(),
                OrderId = orderId,
                InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMddHHmmss}",
                Subtotal = financial.ItemSubtotal
                    + financial.DeliveryFee
                    + financial.ServiceFee
                    + financial.MechanicAmount,
                Tax = financial.Tax,
                Total = financial.TotalAmount,
                Currency = financial.Currency,
                IssuedAt = DateTime.UtcNow
            };

            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync();
        }

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
<title>{WebUtility.HtmlEncode(invoice.InvoiceNumber)}</title>
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
<h1>Alpha Auto Invoice</h1>

<p><strong>Invoice:</strong> {WebUtility.HtmlEncode(invoice.InvoiceNumber)}</p>
<p><strong>Order:</strong> {WebUtility.HtmlEncode(order.OrderNumber)}</p>
<p><strong>Customer:</strong> {WebUtility.HtmlEncode(order.CustomerName)}</p>
<p><strong>Item:</strong> {WebUtility.HtmlEncode(order.ItemDescription)}</p>
<p class='muted'><strong>Issued:</strong> {invoice.IssuedAt:yyyy-MM-dd HH:mm} UTC</p>

<h3 class='section-title'>Charges</h3>
<div class='row'><span>Item Subtotal</span><span>{WebUtility.HtmlEncode(financial.Currency)} {financial.ItemSubtotal:0.00}</span></div>
<div class='row'><span>Delivery Fee</span><span>{WebUtility.HtmlEncode(financial.Currency)} {financial.DeliveryFee:0.00}</span></div>
<div class='row'><span>Service Fee</span><span>{WebUtility.HtmlEncode(financial.Currency)} {financial.ServiceFee:0.00}</span></div>
{RenderMechanicFee(financial.Currency, financial.MechanicAmount)}
{RenderDiscount(financial.Currency, financial.Discount)}

<h3 class='section-title'>Tax Breakdown</h3>
{taxRows}

<div class='row'><span>Total Tax</span><span>{WebUtility.HtmlEncode(financial.Currency)} {financial.Tax:0.00}</span></div>
<div class='row total'><span>Total</span><span>{WebUtility.HtmlEncode(financial.Currency)} {financial.TotalAmount:0.00}</span></div>
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