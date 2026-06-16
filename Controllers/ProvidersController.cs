using Alpha.API.Data;
using Alpha.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace Alpha.API.Controllers;

[ApiController]
[Route("api/providers")]
public class ProvidersController : ControllerBase
{
    private readonly AppDbContext _context;

    public ProvidersController(AppDbContext context)
    {
        _context = context;
    }

    // =====================================================
    // GET ALL PROVIDERS
    // =====================================================

    [HttpGet]
    public async Task<IActionResult> GetProviders()
    {
        var providers = await _context.Suppliers
            .OrderBy(x => x.Name)
            .ToListAsync();

        return Ok(providers);
    }

    // =====================================================
    // GET PROVIDER BY ID
    // =====================================================

    [HttpGet("{providerId}")]
    public async Task<IActionResult> GetProvider(Guid providerId)
    {
        var provider = await _context.Suppliers
            .FirstOrDefaultAsync(x => x.Id == providerId);

        if (provider == null)
            return NotFound();

        return Ok(provider);
    }

    // =====================================================
    // GET PROVIDER ORDERS
    // =====================================================

    [HttpGet("{providerId}/orders")]
    public async Task<IActionResult> GetProviderOrders(Guid providerId)
    {
        var orders = await _context.Orders
            .Include(x => x.Supplier)
            .Include(x => x.Driver)
            .Where(x => x.SupplierId == providerId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.OrderNumber,
                x.CustomerName,
                x.PickupAddress,
                x.DeliveryAddress,
                x.ItemDescription,
                x.Status,
                x.CreatedAt,

                DriverName =
                    x.Driver != null
                        ? x.Driver.FullName
                        : null
            })
            .ToListAsync();

        return Ok(orders);
    }

    // =====================================================
    // ACCEPT ORDER
    // supplier_assigned
    // ->
    // supplier_accepted
    // =====================================================

    [HttpPost("orders/{orderId}/accept")]
    public async Task<IActionResult> AcceptOrder(Guid orderId)
    {
        var order = await _context.Orders
            .Include(x => x.Supplier)
            .FirstOrDefaultAsync(x => x.Id == orderId);

        if (order == null)
            return NotFound();

        order.Status = "supplier_accepted";

        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await AddStatusHistory(
            order.Id,
            "supplier_accepted"
        );

        await AddAuditLog(
            order.Id,
            $"Provider accepted order"
        );

        return Ok(new
        {
            message = "Order accepted",
            status = order.Status
        });
    }

    // =====================================================
    // MARK READY FOR PICKUP
    // supplier_accepted
    // ->
    // ready_for_pickup
    // =====================================================

    [HttpPost("orders/{orderId}/ready")]
    public async Task<IActionResult> MarkReadyForPickup(
        Guid orderId)
    {
        var order = await _context.Orders
            .Include(x => x.Supplier)
            .FirstOrDefaultAsync(x => x.Id == orderId);

        if (order == null)
            return NotFound();

        order.Status = "ready_for_pickup";

        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await AddStatusHistory(
            order.Id,
            "ready_for_pickup"
        );

        await AddAuditLog(
            order.Id,
            "Provider marked order ready"
        );

        return Ok(new
        {
            message = "Order ready for pickup",
            status = order.Status
        });
    }

    // =====================================================
    // REJECT ORDER
    // =====================================================

    [HttpPost("orders/{orderId}/reject")]
    public async Task<IActionResult> RejectOrder(
        Guid orderId)
    {
        var order = await _context.Orders
            .FirstOrDefaultAsync(x => x.Id == orderId);

        if (order == null)
            return NotFound();

        order.Status = "supplier_rejected";

        order.SupplierId = null;

        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await AddStatusHistory(
            order.Id,
            "supplier_rejected"
        );

        await AddAuditLog(
            order.Id,
            "Provider rejected order"
        );

        return Ok(new
        {
            message = "Order rejected"
        });
    }

    // =====================================================
    // DASHBOARD STATS
    // =====================================================

    [HttpGet("{providerId}/dashboard")]
    public async Task<IActionResult> Dashboard(
        Guid providerId)
    {
        var orders = await _context.Orders
            .Where(x => x.SupplierId == providerId)
            .ToListAsync();

        var response = new
        {
            totalOrders =
                orders.Count,

            newOrders =
                orders.Count(x =>
                    x.Status == "supplier_assigned"),

            acceptedOrders =
                orders.Count(x =>
                    x.Status == "supplier_accepted"),

            readyForPickup =
                orders.Count(x =>
                    x.Status == "ready_for_pickup"),

            completed =
                orders.Count(x =>
                    x.Status == "delivered")
        };

        return Ok(response);
    }

    // =====================================================
    // STATUS HISTORY HELPER
    // =====================================================

    private async Task AddStatusHistory(
        Guid orderId,
        string status)
    {
        var history = new StatusHistory
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Status = status,
            CreatedAt = DateTime.UtcNow
        };

        _context.StatusHistory.Add(history);

        await _context.SaveChangesAsync();
    }

    // =====================================================
    // AUDIT LOG HELPER
    // =====================================================

    private async Task AddAuditLog(
        Guid orderId,
        string action)
    {
        var log = new AuditLog
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Action = action,
            CreatedAt = DateTime.UtcNow
        };

        _context.AuditLogs.Add(log);

        await _context.SaveChangesAsync();
    }
}