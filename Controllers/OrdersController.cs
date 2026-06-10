using Alpha.API.Data;
using Alpha.API.DTOs;
using Alpha.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Alpha.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _context;

    public OrdersController(AppDbContext context)
    {
        _context = context;
    }

    // =========================================================
    // CREATE ORDER
    // POST: /api/Orders
    // =========================================================

    [HttpPost]
    public async Task<IActionResult> CreateOrder(CreateOrderDto dto)
    {
        try
        {
            var order = new Order
            {
                Id = Guid.NewGuid(),

                CustomerName = dto.CustomerName,

                PickupAddress = dto.PickupAddress,

                DeliveryAddress = dto.DeliveryAddress,

                ItemDescription = dto.ItemDescription,

                Zone = dto.Zone,

                OrderNumber = $"ALPHA-{DateTime.UtcNow.Ticks}",

                Status = "pending",

                CreatedAt = DateTime.UtcNow,

                UpdatedAt = DateTime.UtcNow
            };

            _context.Orders.Add(order);

            await _context.SaveChangesAsync();

            await AddStatusHistory(order.Id, "pending");

            await AddAuditLog(order.Id, "Order Created");

            return Ok(order);
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.ToString());
        }
    }

    // =========================================================
    // GET ORDER DETAILS
    // GET: /api/Orders/{id}
    // =========================================================

    [HttpGet]
    public async Task<IActionResult> GetOrders()
    {
        var orders = await _context.Orders
            .Include(o => o.Supplier)
            .Include(o => o.Driver)
            .Select(o => new
            {
                o.Id,

                o.OrderNumber,

                o.CustomerName,

                o.PickupAddress,

                o.DeliveryAddress,

                o.ItemDescription,

                o.Zone,

                Status =
                    o.Status == "pending"
                        ? "Pending"
                    : o.Status == "supplier_assigned"
                        ? "Supplier Assigned"
                    : o.Status == "driver_assigned"
                        ? "Driver Assigned"
                    : o.Status == "picked_up"
                        ? "Picked Up"
                    : o.Status == "en_route"
                        ? "En Route"
                    : o.Status == "delivered"
                        ? "Delivered"
                    : o.Status,

                SupplierName =
                    o.Supplier != null
                        ? o.Supplier.Name
                        : null,

                DriverName =
                    o.Driver != null
                        ? o.Driver.FullName
                        : null,

                o.CreatedAt,

                o.UpdatedAt
            })
            .ToListAsync();

        return Ok(orders);
    }
    // =========================================================
    // GET STATUS HISTORY
    // GET: /api/Orders/{id}/status
    // =========================================================

    [HttpGet("{id}/status")]
    public async Task<IActionResult> GetStatus(Guid id)
    {
        var history = await _context.StatusHistory
            .Where(x => x.OrderId == id)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();

        return Ok(history);
    }

    // =========================================================
    // ASSIGN SUPPLIER
    // POST: /api/Orders/{id}/assign-supplier
    // =========================================================
    [HttpPost("{id}/assign-supplier")]
    public async Task<IActionResult> AssignSupplier(Guid id)
    {
        var order = await _context.Orders
            .FindAsync(id);

        if (order == null)
            return NotFound();

        // Find AVAILABLE supplier
        var supplier = await _context.Suppliers
            .Where(s => s.AvailabilityStatus == "available")
            .FirstOrDefaultAsync();

        if (supplier == null)
            return BadRequest("No available suppliers");

        // Assign supplier
        order.SupplierId = supplier.Id;

        // Change supplier status
        supplier.AvailabilityStatus = "busy";

        // Update order status
        order.Status = "supplier_assigned";

        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await AddStatusHistory(
            id,
            "supplier_assigned"
        );

        await AddAuditLog(
            id,
            $"Supplier Assigned: {supplier.Name}"
        );

        return Ok(new
        {
            message = "Supplier assigned successfully",
            supplier = supplier.Name,
            supplierId = supplier.Id,
            orderStatus = order.Status
        });
    }

    // =========================================================
    // ASSIGN DRIVER
    // POST: /api/Orders/{id}/assign-driver
    // =========================================================

    [HttpPost("{id}/assign-driver")]
    public async Task<IActionResult> AssignDriver(Guid id)
    {
        var order = await _context.Orders
            .FindAsync(id);

        if (order == null)
            return NotFound();

        // Find AVAILABLE driver
        var driver = await _context.Drivers
            .Where(d => d.AvailabilityStatus == "available")
            .FirstOrDefaultAsync();

        if (driver == null)
            return BadRequest("No available drivers");

        // Assign driver
        order.DriverId = driver.Id;

        // Change driver status
        driver.AvailabilityStatus = "busy";

        // Update order status
        order.Status = "driver_assigned";

        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await AddStatusHistory(
            id,
            "driver_assigned"
        );

        await AddAuditLog(
            id,
            $"Driver Assigned: {driver.FullName}"
        );

        return Ok(new
        {
            message = "Driver assigned successfully",
            driver = driver.FullName,
            driverId = driver.Id,
            orderStatus = order.Status
        });
    }

    // =========================================================
    // PICKED UP
    // POST: /api/Orders/{id}/picked-up
    // =========================================================

    [HttpPost("{id}/picked-up")]
    public async Task<IActionResult> PickedUp(Guid id)
    {
        var order = await _context.Orders.FindAsync(id);

        if (order == null)
            return NotFound();

        order.Status = "picked_up";

        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await AddStatusHistory(id, "picked_up");

        await AddAuditLog(id, "Order Picked Up");

        return Ok(new
        {
            message = "Order marked as picked up",
            status = order.Status
        });
    }

    // =========================================================
    // EN ROUTE
    // POST: /api/Orders/{id}/en-route
    // =========================================================

    [HttpPost("{id}/en-route")]
    public async Task<IActionResult> EnRoute(Guid id)
    {
        var order = await _context.Orders.FindAsync(id);

        if (order == null)
            return NotFound();

        order.Status = "en_route";

        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await AddStatusHistory(id, "en_route");

        await AddAuditLog(id, "Order En Route");

        return Ok(new
        {
            message = "Order is now en route",
            status = order.Status
        });
    }

    // =========================================================
    // DELIVERED
    // POST: /api/Orders/{id}/delivered
    // =========================================================
    [HttpPost("{id}/delivered")]
    public async Task<IActionResult> Delivered(Guid id)
    {
        var order = await _context.Orders
            .Include(o => o.Driver)
            .Include(o => o.Supplier)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
            return NotFound();

        order.Status = "delivered";

        order.UpdatedAt = DateTime.UtcNow;

        // Free driver
        if (order.Driver != null)
        {
            order.Driver.AvailabilityStatus = "available";
        }

        // Free supplier
        if (order.Supplier != null)
        {
            order.Supplier.AvailabilityStatus = "available";
        }

        await _context.SaveChangesAsync();

        await AddStatusHistory(id, "delivered");

        await AddAuditLog(id, "Order Delivered");

        return Ok(new
        {
            message = "Order delivered successfully",
            status = order.Status
        });
    }

    // =========================================================
    // UPLOAD DELIVERY PROOF
    // POST: /api/Orders/{id}/proof
    // =========================================================

    [HttpPost("{id}/proof")]
    public async Task<IActionResult> UploadProof(Guid id, string imageUrl)
    {
        var order = await _context.Orders.FindAsync(id);

        if (order == null)
            return NotFound();

        var proof = new DeliveryProof
        {
            Id = Guid.NewGuid(),

            OrderId = id,

            ImageUrl = imageUrl,

            UploadedAt = DateTime.UtcNow
        };

        _context.DeliveryProofs.Add(proof);

        order.Status = "proof_uploaded";

        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await AddStatusHistory(id, "proof_uploaded");

        await AddAuditLog(id, "Delivery Proof Uploaded");

        return Ok(proof);
    }

    // =========================================================
    // HELPER: STATUS HISTORY
    // =========================================================

    private async Task AddStatusHistory(Guid orderId, string status)
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

    // =========================================================
    // HELPER: AUDIT LOG
    // =========================================================

    private async Task AddAuditLog(Guid orderId, string action)
    {
        var log = new AuditLog
        {
            Id = Guid.NewGuid(),

            OrderId = orderId,

            Action = action,

            PerformedBy = "System",

            CreatedAt = DateTime.UtcNow
        };

        _context.AuditLogs.Add(log);

        await _context.SaveChangesAsync();
    }
}