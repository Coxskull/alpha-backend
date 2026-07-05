using Alpha.API.Data;
using Alpha.API.DTOs;
using Alpha.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Alpha.API.Constants;
using Alpha.API.Services;
using System.IO;
using Microsoft.AspNetCore.Http;

namespace Alpha.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly OrderWorkflowService _workflow;
    private readonly SettlementService _settlements;
    private readonly TaxEngineService _taxEngine;

    public OrdersController(
        AppDbContext context,
        OrderWorkflowService workflow,
        SettlementService settlements,
        TaxEngineService taxEngine)
    {
        _context = context;
        _workflow = workflow;
        _settlements = settlements;
        _taxEngine = taxEngine;
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

            if (dto.Items == null || !dto.Items.Any())
                return BadRequest("Cart is empty.");

            var productIds = dto.Items.Select(x => x.ProductId).ToList();

            var products = await _context.Products
                .Where(x => productIds.Contains(x.Id) && x.IsActive)
                .ToListAsync();

            if (products.Count != productIds.Count)
                return BadRequest("One or more products are invalid.");

            foreach (var item in dto.Items)
            {
                var product = products.First(x => x.Id == item.ProductId);

                if (item.Quantity <= 0)
                    return BadRequest("Quantity must be greater than 0.");

                if (product.QuantityAvailable < item.Quantity)
                    return BadRequest($"{product.Name} does not have enough stock.");
            }

            decimal itemSubtotal = dto.Items.Sum(item =>
            {
                var product = products.First(x => x.Id == item.ProductId);
                return product.Price * item.Quantity;
            });

            decimal deliveryFee = 8.00m;
            decimal serviceFee = 3.00m;
            decimal tax = 0;
            decimal discount = 0;
            decimal totalAmount = 0;

            decimal exchangeRate = currency == "MXN" ? 17.00m : 1.00m;
            decimal supplierEarning = itemSubtotal * 0.80m;
            decimal driverEarning = deliveryFee * 0.70m;
            decimal companyRevenue =
                serviceFee +
                (itemSubtotal * 0.20m) +
                (deliveryFee * 0.30m);

            var order = new Order
            {
                Id = Guid.NewGuid(),
                CustomerName = dto.CustomerName,
                PickupAddress = dto.PickupAddress,
                DeliveryAddress = dto.DeliveryAddress,
                ItemDescription = dto.ItemDescription,
                Zone = dto.Zone,
                OrderNumber = $"ALPHA-{DateTime.UtcNow.Ticks}",
                Status = OrderStatuses.PaymentPending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Orders.Add(order);

            // Save order first so order_financials.order_id has a valid parent order.
            await _context.SaveChangesAsync();

            foreach (var item in dto.Items)
            {
                var product = products.First(x => x.Id == item.ProductId);

                _context.OrderItems.Add(new OrderItem
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    ProductId = product.Id,
                    Quantity = item.Quantity,
                    UnitPrice = product.Price
                });

                product.QuantityAvailable -= item.Quantity;
            }

            var financial = new OrderFinancial
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                Currency = currency,
                ExchangeRate = exchangeRate,

                ItemSubtotal = itemSubtotal,
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
            await _context.SaveChangesAsync();

            var country = "MX";

            var taxBreakdown = await _taxEngine.CalculateOrderTaxes(
                order.Id,
                country,
                dto.Zone,
                currency
            );

            financial = await _context.OrderFinancials
                .FirstAsync(x => x.OrderId == order.Id);

            totalAmount = financial.TotalAmount;
            tax = financial.Tax;

            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                Amount = financial.TotalAmount,
                Currency = currency,
                PaymentMethod = dto.PaymentMethod,
                PaymentStatus = dto.PaymentMethod == "paypal"
        ? "pending"
        : "cash_pending",
                CreatedAt = DateTime.UtcNow
            };

            _context.Payments.Add(payment);

            await _context.SaveChangesAsync();

            await AddStatusHistory(order.Id, OrderStatuses.PaymentPending);
            await AddAuditLog(order.Id, "Order Created with Financials");

            return Ok(new
            {
                order,
                financial,
                payment,
                taxBreakdown,
                items = dto.Items.Select(item =>
                {
                    var product = products.First(x => x.Id == item.ProductId);

                    return new
                    {
                        productId = product.Id,
                        productName = product.Name,
                        quantity = item.Quantity,
                        unitPrice = product.Price,
                        lineTotal = product.Price * item.Quantity
                    };
                })
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
                .GroupJoin(
                    _context.DeliveryProofs,
                    order => order.Id,
                    proof => proof.OrderId,
                    (order, proofs) => new
                    {
                        Order = order,
                        Proof = proofs
                            .OrderByDescending(p => p.UploadedAt)
                            .FirstOrDefault()
                    }
                )
                .OrderByDescending(x => x.Order.CreatedAt)
                .Select(x => new
                {
                    id = x.Order.Id,
                    orderNumber = x.Order.OrderNumber,
                    customerName = x.Order.CustomerName,
                    pickupAddress = x.Order.PickupAddress,
                    deliveryAddress = x.Order.DeliveryAddress,
                    itemDescription = x.Order.ItemDescription,
                    zone = x.Order.Zone,
                    status = x.Order.Status,
                    createdAt = x.Order.CreatedAt,
                    updatedAt = x.Order.UpdatedAt,

                    supplierId = x.Order.SupplierId,
                    supplierName = x.Order.Supplier != null
                        ? x.Order.Supplier.Name
                        : null,

                    driverId = x.Order.DriverId,
                    driverName = x.Order.Driver != null
                        ? x.Order.Driver.FullName
                        : null,

                    proofImageUrl = x.Proof != null
                        ? x.Proof.ImageUrl
                        : null,

                    proofUploadedAt = x.Proof != null
                        ? x.Proof.UploadedAt
                        : (DateTime?)null
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

        if (order.Status != OrderStatuses.WaitingForSupplier &&
    order.Status != OrderStatuses.PaymentPaid)
        {
            return BadRequest($"Supplier cannot be assigned while order status is {order.Status}.");
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

        order.Status = OrderStatuses.SupplierAssigned;
        order.UpdatedAt = DateTime.UtcNow;

        supplier.AvailabilityStatus = "busy";

        supplier.CurrentWorkload++;

        await _context.SaveChangesAsync();

        // Optional if you already have these methods
        await AddStatusHistory(order.Id, OrderStatuses.SupplierAssigned);

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

        if (order.Status != OrderStatuses.WaitingForSupplier &&
    order.Status != OrderStatuses.PaymentPaid)
        {
            return BadRequest($"Supplier cannot be assigned while order status is {order.Status}.");
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
        order.Status = OrderStatuses.SupplierAssigned;
        order.UpdatedAt = DateTime.UtcNow;

        supplier.AvailabilityStatus = "busy";
        supplier.CurrentWorkload += 1;

        await _context.SaveChangesAsync();

        await AddStatusHistory(order.Id, OrderStatuses.SupplierAssigned);
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

        if (order.Status != OrderStatuses.WaitingForDriver &&
    order.Status != OrderStatuses.SupplierAccepted)
        {
            return BadRequest(
                $"Driver cannot be assigned while order status is {order.Status}."
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

        order.Status = OrderStatuses.DriverAssigned;

        order.UpdatedAt = DateTime.UtcNow;

        driver.AvailabilityStatus = "busy";

        driver.ActiveJobs++;

        await _context.SaveChangesAsync();

        await AddStatusHistory(order.Id, OrderStatuses.DriverAssigned);

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

        if (order.Status != OrderStatuses.WaitingForDriver &&
     order.Status != OrderStatuses.SupplierAccepted)
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
        order.Status = OrderStatuses.DriverAssigned;
        order.UpdatedAt = DateTime.UtcNow;

        driver.AvailabilityStatus = "busy";
        driver.ActiveJobs += 1;

        await _context.SaveChangesAsync();

        await AddStatusHistory(order.Id, OrderStatuses.DriverAssigned);
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

        if (order.Status != OrderStatuses.WaitingForPickup &&
    order.Status != OrderStatuses.DriverAccepted)
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
        order.Status = OrderStatuses.PickedUp;
        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await AddStatusHistory(id, OrderStatuses.PickedUp);
        await AddAuditLog(id, "Order Picked Up");

        order.Status = OrderStatuses.EnRoute;
        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await AddStatusHistory(id, OrderStatuses.EnRoute);
        await AddAuditLog(id, "Driver En Route");

        return Ok(new
        {
            message = "Order picked up and driver is now en route",
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

        if (order.Status != OrderStatuses.PickedUp)
        {
            return BadRequest(
                "Order must be picked up first."
            );
        }

        order.Status = OrderStatuses.EnRoute;

        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await AddStatusHistory(id, OrderStatuses.EnRoute);

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

        if (order.Status != OrderStatuses.EnRoute)
            return BadRequest("Order must be en route before delivery.");

        order.Status = OrderStatuses.Delivered;
        order.UpdatedAt = DateTime.UtcNow;

        if (order.Driver != null)
        {
            order.Driver.ActiveJobs--;

            if (order.Driver.ActiveJobs < 0)
                order.Driver.ActiveJobs = 0;

            order.Driver.AvailabilityStatus = "available";
        }

        if (order.Supplier != null)
        {
            order.Supplier.CurrentWorkload--;

            if (order.Supplier.CurrentWorkload < 0)
                order.Supplier.CurrentWorkload = 0;

            order.Supplier.AvailabilityStatus = "available";
        }

        await _context.SaveChangesAsync();

        await AddStatusHistory(id, OrderStatuses.Delivered);
        await AddAuditLog(id, "Order Delivered");

        return Ok(new
        {
            message = "Order delivered successfully. Waiting for proof upload.",
            status = order.Status
        });
    }

    // =========================================================
    // UPLOAD DELIVERY PROOF
    // POST: /api/Orders/{id}/proof
    // =========================================================

    [HttpPost("{id}/proof")]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> UploadProof(Guid id, [FromForm] IFormFile image)
    {
        var order = await _context.Orders.FindAsync(id);

        if (order == null)
            return NotFound("Order not found.");

        if (order.Status != OrderStatuses.Delivered)
            return BadRequest("Order must be delivered first.");

        if (image == null || image.Length == 0)
            return BadRequest("Image required.");

        await using var ms = new MemoryStream();
        await image.CopyToAsync(ms);

        var base64 = Convert.ToBase64String(ms.ToArray());
        var imageUrl = $"data:{image.ContentType};base64,{base64}";

        var proof = new DeliveryProof
        {
            Id = Guid.NewGuid(),
            OrderId = id,
            ImageUrl = imageUrl,
            UploadedAt = DateTime.UtcNow
        };

        _context.DeliveryProofs.Add(proof);

        order.Status = OrderStatuses.ProofUploaded;
        order.UpdatedAt = DateTime.UtcNow;

        var financial = await _context.OrderFinancials
            .FirstOrDefaultAsync(x => x.OrderId == id);

        if (financial != null)
        {
            financial.CompletionProofUrl = imageUrl;
            financial.FinancialStatus = "verified";
            financial.PayoutStatus = "ready_for_payout";
            financial.SettlementStatus = "ready_for_payout";
        }

        await _context.SaveChangesAsync();

        await AddStatusHistory(id, OrderStatuses.ProofUploaded);
        await AddAuditLog(id, "Delivery Proof Uploaded");

        try
        {
            await _settlements.VerifySettlementAfterProof(id);
        }
        catch (Exception settlementError)
        {
            await AddAuditLog(
                id,
                $"Proof uploaded, but settlement verification failed: {settlementError.Message}"
            );
        }

        return Ok(new
        {
            proof,
            imageUrl,
            status = OrderStatuses.ProofUploaded,
            message = "Proof uploaded successfully."
        });
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

        order.Status = OrderStatuses.WaitingForSupplier;
        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await AddStatusHistory(id, OrderStatuses.PaymentPaid);
        await AddStatusHistory(id, OrderStatuses.WaitingForSupplier);
        await AddAuditLog(id, "Payment Confirmed");

        return Ok(new
        {
            message = "Payment confirmed. Order is now paid and pending dispatch.",
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

        order.Status = OrderStatuses.SupplierAccepted;
        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await AddStatusHistory(id, OrderStatuses.SupplierAccepted);
        await AddAuditLog(id, "Supplier Accepted Order");

        order.Status = OrderStatuses.WaitingForDriver;
        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await AddStatusHistory(id, OrderStatuses.WaitingForDriver);
        await AddAuditLog(id, "Order Waiting For Driver");

        return Ok(order);
    }
    [HttpPost("{id}/ready-for-pickup")]
    public async Task<IActionResult> ReadyForPickup(Guid id)
    {
        var order = await _context.Orders.FindAsync(id);

        if (order == null)
            return NotFound();

        order.Status = OrderStatuses.WaitingForPickup;
        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await AddStatusHistory(id, OrderStatuses.WaitingForPickup);
        await AddAuditLog(id, "Order Ready For Pickup");

        return Ok(order);
    }
    [HttpPost("{id}/driver-accept")]
    public async Task<IActionResult> DriverAccept(Guid id)
    {
        var order = await _context.Orders.FindAsync(id);

        if (order == null)
            return NotFound();

        order.Status = OrderStatuses.DriverAccepted;
        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await AddStatusHistory(id, OrderStatuses.DriverAccepted);
        await AddAuditLog(id, "Driver Accepted Order");

        order.Status = OrderStatuses.WaitingForPickup;
        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await AddStatusHistory(id, OrderStatuses.WaitingForPickup);
        await AddAuditLog(id, "Order Waiting For Pickup");

        return Ok(order);
    }

}
