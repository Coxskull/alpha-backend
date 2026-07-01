using Alpha.API.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
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

        var html = $@"
<!doctype html>
<html>
<head>
<title>Receipt - {order.OrderNumber}</title>
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
<h1>Alpha Auto Receipt</h1>
<p><strong>Order:</strong> {order.OrderNumber}</p>
<p><strong>Customer:</strong> {order.CustomerName}</p>
<p><strong>Payment Method:</strong> {payment.PaymentMethod}</p>
<p><strong>Transaction:</strong> {payment.TransactionReference}</p>
<p><strong>Paid At:</strong> {payment.PaidAt}</p>
<div class='row total'><span>Amount Paid</span><span>{financial.Currency} {financial.TotalAmount:0.00}</span></div>
</div>
</body>
</html>";

        return Content(html, "text/html");
    }
}