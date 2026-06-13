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
        try
        {
            var orders = await _context.Orders
    .Include(o => o.Supplier)
    .Include(o => o.Driver)
    .OrderByDescending(o => o.CreatedAt)
    .Select(o => new
    {
        id = o.Id,

        orderNumber = o.OrderNumber,

        customerName = o.CustomerName,

        pickupAddress = o.PickupAddress,

        deliveryAddress = o.DeliveryAddress,

        itemDescription = o.ItemDescription,

        zone = o.Zone,

        status = o.Status,

        createdAt = o.CreatedAt,

        updatedAt = o.UpdatedAt,

        supplierId = o.SupplierId,

        supplierName = o.Supplier != null
            ? o.Supplier.Name
            : null,

        driverId = o.DriverId,

        driverName = o.Driver != null
            ? o.Driver.FullName
            : null
    })
    .ToListAsync();

            return Ok(orders);
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.ToString());
        }
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
    [HttpPost("{id}/assign-supplier/{supplierId}")]
    public async Task<IActionResult> AssignSupplier(
    Guid id,
    Guid supplierId)
    {
        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
            return NotFound("Order not found.");

        if (order.Status != "pending")
        {
            return BadRequest(
                "Supplier can only be assigned when order is Pending."
            );
        }

        var supplier = await _context.Suppliers
            .FirstOrDefaultAsync(s => s.Id == supplierId);

        if (supplier == null)
            return NotFound("Supplier not found.");

        if (supplier.AvailabilityStatus != "available")
        {
            return BadRequest(
                "Selected supplier is not available."
            );
        }

        order.SupplierId = supplier.Id;

        order.Status = "supplier_assigned";
        order.UpdatedAt = DateTime.UtcNow;

        supplier.AvailabilityStatus = "busy";

        await _context.SaveChangesAsync();

        // Optional if you already have these methods
        await AddStatusHistory(
            order.Id,
            "supplier_assigned"
        );

        await AddAuditLog(
            order.Id,
            $"Supplier Assigned: {supplier.Name}"
        );

        return Ok(new
        {
            message = "Supplier assigned successfully",
            supplierId = supplier.Id,
            supplierName = supplier.Name,
            status = order.Status
        });
    }

    // =========================================================
    // ASSIGN DRIVER
    // POST: /api/Orders/{id}/assign-driver
    // =========================================================

    [HttpPost("{id}/assign-driver/{driverId}")]
    public async Task<IActionResult> AssignDriver(
    Guid id,
    Guid driverId)
    {
        var order = await _context.Orders
            .FirstOrDefaultAsync(x => x.Id == id);

        if (order == null)
            return NotFound();

        if (order.Status != "supplier_assigned")
        {
            return BadRequest(
                "Driver can only be assigned after supplier assignment."
            );
        }

        var driver = await _context.Drivers
            .FirstOrDefaultAsync(x => x.Id == driverId);

        if (driver == null)
            return NotFound();

        if (driver.AvailabilityStatus != "available")
        {
            return BadRequest(
                "Driver is not available."
            );
        }

        order.DriverId = driver.Id;

        order.Status = "driver_assigned";

        order.UpdatedAt = DateTime.UtcNow;

        driver.AvailabilityStatus = "busy";

        await _context.SaveChangesAsync();

        await AddStatusHistory(
            order.Id,
            "driver_assigned"
        );

        await AddAuditLog(
            order.Id,
            $"Driver Assigned: {driver.FullName}"
        );

        return Ok(new
        {
            message = "Driver assigned successfully",
            driverId = driver.Id,
            driverName = driver.FullName,
            status = order.Status
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

        if (order.Status != "driver_assigned")
        {
            return BadRequest(
                "Order must have assigned driver before pickup."
            );
        }

        if (order.DriverId == null)
        {
            return BadRequest(
                "No driver assigned."
            );
        }

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

        if (order.Status != "picked_up")
        {
            return BadRequest(
                "Order must be picked up first."
            );
        }

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

        if (order.Status != "en_route")
        {
            return BadRequest(
                "Order must be en route before delivery."
            );
        }

        order.Status = "delivered";

        order.UpdatedAt = DateTime.UtcNow;

        if (order.Driver != null)
        {
            order.Driver.AvailabilityStatus = "available";
        }

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

    private async Task AddAuditLog(
    Guid orderId,
    string action,
    string performedBy = "System")
    {
        var log = new AuditLog
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Action = action,
            PerformedBy = performedBy,
            CreatedAt = DateTime.UtcNow
        };

        _context.AuditLogs.Add(log);

        await _context.SaveChangesAsync();
    }
    [HttpGet("{id}/details")]
    public async Task<IActionResult> GetDetails(Guid id)
    {
        var order = await _context.Orders
            .Include(x => x.Supplier)
            .Include(x => x.Driver)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (order == null)
            return NotFound();

        return Ok(new
        {
            id = order.Id,

            orderNumber = order.OrderNumber,

            customerName = order.CustomerName,

            itemDescription = order.ItemDescription,

            pickupAddress = order.PickupAddress,

            deliveryAddress = order.DeliveryAddress,

            zone = order.Zone,

            status = order.Status,

            supplierId = order.SupplierId,

            supplierName = order.Supplier != null
                ? order.Supplier.Name
                : null,

            driverId = order.DriverId,

            driverName = order.Driver != null
                ? order.Driver.FullName
                : null,

            createdAt = order.CreatedAt,

            updatedAt = order.UpdatedAt
        });
    }

}
