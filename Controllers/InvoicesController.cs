using Alpha.API.Data;
using Alpha.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
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
            Subtotal = financial.ItemSubtotal + financial.DeliveryFee + financial.ServiceFee,
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

        invoice ??= new Invoice
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMddHHmmss}",
            Subtotal = financial.ItemSubtotal + financial.DeliveryFee + financial.ServiceFee,
            Tax = financial.Tax,
            Total = financial.TotalAmount,
            Currency = financial.Currency,
            IssuedAt = DateTime.UtcNow
        };

        if (_context.Entry(invoice).State == EntityState.Detached)
        {
            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync();
        }

        var html = $@"
<!doctype html>
<html>
<head>
<title>{invoice.InvoiceNumber}</title>
<style>
body {{ font-family: Arial; padding: 40px; }}
.card {{ border: 1px solid #ddd; padding: 24px; border-radius: 12px; }}
.row {{ display:flex; justify-content:space-between; margin:8px 0; }}
.total {{ font-size:22px; font-weight:bold; border-top:1px solid #ddd; padding-top:12px; }}
@media print {{ button {{ display:none; }} }}
</style>
</head>
<body>
<button onclick='window.print()'>Download / Print PDF</button>
<div class='card'>
<h1>Alpha Auto Invoice</h1>
<p><strong>Invoice:</strong> {invoice.InvoiceNumber}</p>
<p><strong>Order:</strong> {order.OrderNumber}</p>
<p><strong>Customer:</strong> {order.CustomerName}</p>
<p><strong>Item:</strong> {order.ItemDescription}</p>

<div class='row'><span>Item Subtotal</span><span>{financial.Currency} {financial.ItemSubtotal:0.00}</span></div>
<div class='row'><span>Delivery Fee</span><span>{financial.Currency} {financial.DeliveryFee:0.00}</span></div>
<div class='row'><span>Service Fee</span><span>{financial.Currency} {financial.ServiceFee:0.00}</span></div>
<div class='row'><span>Tax</span><span>{financial.Currency} {financial.Tax:0.00}</span></div>
<div class='row total'><span>Total</span><span>{financial.Currency} {financial.TotalAmount:0.00}</span></div>
</div>
</body>
</html>";

        return Content(html, "text/html");
    }
}