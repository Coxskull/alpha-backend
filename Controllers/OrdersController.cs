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
            var currency = string.IsNullOrWhiteSpace(dto.Currency)
                ? "USD"
                : dto.Currency.ToUpper();

            if (currency != "USD" && currency != "MXN")
                return BadRequest("Currency must be USD or MXN.");

            decimal exchangeRate = currency == "USD" ? 1 : 17.59m;

            decimal deliveryFee = currency == "USD" ? 8.00m : 8.00m * exchangeRate;
            decimal serviceFee = currency == "USD" ? 3.00m : 3.00m * exchangeRate;
            decimal tax = dto.ItemSubtotal * 0.08m;
            decimal discount = 0;

            decimal totalAmount =
                dto.ItemSubtotal +
                deliveryFee +
                serviceFee +
                tax -
                discount;

            decimal driverEarning = deliveryFee * 0.80m;
            decimal supplierEarning = dto.ItemSubtotal;
            decimal companyRevenue = serviceFee + (deliveryFee - driverEarning);

            var order = new Order
            {
                Id = Guid.NewGuid(),
                CustomerName = dto.CustomerName,
                PickupAddress = dto.PickupAddress,
                DeliveryAddress = dto.DeliveryAddress,
                ItemDescription = dto.ItemDescription,
                Zone = dto.Zone,
                OrderNumber = $"ALPHA-{DateTime.UtcNow.Ticks}",
                Status = "payment_pending",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Orders.Add(order);

            // IMPORTANT:
            // Save order first so FK order_financials.order_id exists.
            await _context.SaveChangesAsync();

            var financial = new OrderFinancial
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                Currency = currency,
                ExchangeRate = exchangeRate,

                ItemSubtotal = dto.ItemSubtotal,
                DeliveryFee = deliveryFee,
                ServiceFee = serviceFee,
                Tax = tax,
                Discount = discount,
                TotalAmount = totalAmount,

                CustomerPaid = 0,
                SupplierAmount = supplierEarning,
                DriverAmount = driverEarning,
                MechanicAmount = 0,
                AlphaPlatformFee = companyRevenue,

                SupplierEarning = supplierEarning,
                DriverEarning = driverEarning,
                CompanyRevenue = companyRevenue,

                FinancialStatus = dto.PaymentMethod == "paypal"
         ? "awaiting_payment"
         : "pending_review",

                PayoutStatus = "not_ready",
                CreatedAt = DateTime.UtcNow
            };

            _context.OrderFinancials.Add(financial);

            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                Amount = totalAmount,
                Currency = currency,
                PaymentMethod = dto.PaymentMethod,
                PaymentStatus = dto.PaymentMethod == "paypal"
        ? "pending"
        : "cash_pending",
                CreatedAt = DateTime.UtcNow
            };

            _context.Payments.Add(payment);

            await _context.SaveChangesAsync();

            await AddStatusHistory(order.Id, "payment_pending");
            await AddAuditLog(order.Id, "Order Created with Financials");

            return Ok(new
            {
                order,
                financial,
                payment
            });
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

        if (
    order.Status != "pending" &&
    order.Status != "payment_confirmed" &&
    order.Status != "payment_paid"
)
        {
            return BadRequest(
                $"Supplier cannot be assigned while order status is {order.Status}."
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

        supplier.CurrentWorkload++;

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

    [HttpPost("{id}/assign-supplier")]
    public async Task<IActionResult> AssignSupplierAuto(Guid id)
    {
        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
            return NotFound("Order not found.");

        if (
            order.Status != "pending" &&
            order.Status != "payment_confirmed" &&
            order.Status != "payment_paid"
        )
        {
            return BadRequest(
                $"Supplier cannot be assigned while order status is {order.Status}."
            );
        }

        var supplier = await _context.Suppliers
            .Where(s =>
                s.AvailabilityStatus.ToLower() == "available" &&
                s.Territory == order.Zone)
            .OrderBy(s => s.CurrentWorkload)
            .ThenByDescending(s => s.ResponseRate)
            .FirstOrDefaultAsync();

        supplier ??= await _context.Suppliers
            .Where(s => s.AvailabilityStatus.ToLower() == "available")
            .OrderBy(s => s.CurrentWorkload)
            .ThenByDescending(s => s.ResponseRate)
            .FirstOrDefaultAsync();

        if (supplier == null)
            return NotFound("No available supplier found.");

        order.SupplierId = supplier.Id;
        order.Status = "supplier_assigned";
        order.UpdatedAt = DateTime.UtcNow;

        supplier.AvailabilityStatus = "busy";
        supplier.CurrentWorkload += 1;

        await _context.SaveChangesAsync();

        await AddStatusHistory(order.Id, "supplier_assigned");
        await AddAuditLog(order.Id, $"Auto Supplier Assigned: {supplier.Name}");

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

        if (order.Status != "ready_for_pickup")
        {
            return BadRequest(
                "Order must be ready for pickup before assigning a driver."
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

        driver.ActiveJobs++;

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

    [HttpPost("{id}/assign-driver")]
    public async Task<IActionResult> AssignDriverAuto(Guid id)
    {
        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
            return NotFound("Order not found.");

        if (order.SupplierId == null)
            return BadRequest("Assign supplier first.");

        if (
            order.Status != "supplier_assigned" &&
            order.Status != "ready_for_pickup" &&
            order.Status != "supplier_accepted"
        )
        {
            return BadRequest(
                $"Driver cannot be assigned while order status is {order.Status}."
            );
        }

        var driver = await _context.Drivers
            .Where(d =>
                d.AvailabilityStatus.ToLower() == "available" &&
                d.Territory == order.Zone)
            .OrderBy(d => d.ActiveJobs)
            .ThenByDescending(d => d.ResponseRate)
            .FirstOrDefaultAsync();

        driver ??= await _context.Drivers
            .Where(d => d.AvailabilityStatus.ToLower() == "available")
            .OrderBy(d => d.ActiveJobs)
            .ThenByDescending(d => d.ResponseRate)
            .FirstOrDefaultAsync();

        if (driver == null)
            return NotFound("No available driver found.");

        order.DriverId = driver.Id;
        order.Status = "driver_assigned";
        order.UpdatedAt = DateTime.UtcNow;

        driver.AvailabilityStatus = "busy";
        driver.ActiveJobs += 1;

        await _context.SaveChangesAsync();

        await AddStatusHistory(order.Id, "driver_assigned");
        await AddAuditLog(order.Id, $"Auto Driver Assigned: {driver.FullName}");

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
            order.Driver.ActiveJobs--;

            if (order.Driver.ActiveJobs < 0)
                order.Driver.ActiveJobs = 0;

            order.Driver.AvailabilityStatus =
                "available";
        }

        if (order.Supplier != null)
        {
            order.Supplier.CurrentWorkload--;

            if (order.Supplier.CurrentWorkload < 0)
                order.Supplier.CurrentWorkload = 0;

            order.Supplier.AvailabilityStatus =
                "available";
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

        order.Status = "delivered";
        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await AddStatusHistory(id, "proof_uploaded");
        await AddStatusHistory(id, "delivered");

        await AddAuditLog(id, "Delivery Proof Uploaded");
        await AddAuditLog(id, "Order Delivered With Proof");

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
    [HttpGet("{id}/recommendations")]
    public async Task<IActionResult> GetRecommendations(Guid id)
    {
        var order = await _context.Orders
            .FirstOrDefaultAsync(x => x.Id == id);

        if (order == null)
            return NotFound();

        var supplier =
            await _context.Suppliers
                .Where(x =>
                    x.AvailabilityStatus == "available" &&
                    x.Territory == order.Zone)
                .OrderBy(x => x.CurrentWorkload)
                .FirstOrDefaultAsync();

        var driver =
            await _context.Drivers
                .Where(x =>
                    x.AvailabilityStatus == "available" &&
                    x.Territory == order.Zone)
                .OrderBy(x => x.ActiveJobs)
                .FirstOrDefaultAsync();

        return Ok(new
        {
            supplier,
            driver
        });
    }
    [HttpPost("{id}/confirm-payment")]
    public async Task<IActionResult> ConfirmPayment(Guid id, string transactionReference)
    {
        var order = await _context.Orders.FindAsync(id);

        if (order == null)
            return NotFound();

        var payment = await _context.Payments
            .FirstOrDefaultAsync(x => x.OrderId == id);

        if (payment == null)
            return NotFound("Payment record not found.");

        payment.PaymentStatus = "paid";
        payment.TransactionReference = transactionReference;
        payment.PaidAt = DateTime.UtcNow;

        order.Status = "pending";
        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await AddStatusHistory(id, "payment_paid");
        await AddStatusHistory(id, "pending");
        await AddAuditLog(id, "Payment Confirmed");

        return Ok(new
        {
            message = "Payment confirmed. Order is now pending dispatch.",
            orderStatus = order.Status,
            paymentStatus = payment.PaymentStatus
        });
    }
    [HttpPost("{id}/supplier-accept")]
    public async Task<IActionResult> SupplierAccept(Guid id)
    {
        var order = await _context.Orders.FindAsync(id);

        if (order == null)
            return NotFound();

        order.Status = "supplier_accepted";
        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await AddStatusHistory(id, "supplier_accepted");
        await AddAuditLog(id, "Supplier Accepted Order");

        return Ok(order);
    }
    [HttpPost("{id}/ready-for-pickup")]
    public async Task<IActionResult> ReadyForPickup(Guid id)
    {
        var order = await _context.Orders.FindAsync(id);

        if (order == null)
            return NotFound();

        order.Status = "ready_for_pickup";
        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await AddStatusHistory(id, "ready_for_pickup");
        await AddAuditLog(id, "Order Ready For Pickup");

        return Ok(order);
    }
    [HttpPost("{id}/driver-accept")]
    public async Task<IActionResult> DriverAccept(Guid id)
    {
        var order = await _context.Orders.FindAsync(id);

        if (order == null)
            return NotFound();

        order.Status = "driver_accepted";
        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await AddStatusHistory(id, "driver_accepted");
        await AddAuditLog(id, "Driver Accepted Order");

        return Ok(order);
    }

}
