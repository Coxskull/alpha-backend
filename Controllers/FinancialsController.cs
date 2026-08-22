using Alpha.API.Data;
using Alpha.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;
using Alpha.API.Services.Entrepreneur;
using Alpha.API.Services;
namespace Alpha.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "admin,dispatcher")]
public class FinancialsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly EntrepreneurCommissionService _entrepreneurCommissionService;
    private readonly SettlementService _settlementService;

    public FinancialsController(
     AppDbContext context,
     EntrepreneurCommissionService entrepreneurCommissionService,
     SettlementService settlementService)
    {
        _context = context;
        _entrepreneurCommissionService = entrepreneurCommissionService;
        _settlementService = settlementService;
    }

    [HttpGet("settlement-queue")]
    public async Task<IActionResult> GetSettlementQueue()
    {
        var queue = await _context.SettlementQueue
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.OrderFinancialId,
                x.PayeeType,
                x.PayeeId,
                x.Amount,
                x.Status,
                x.ReviewedBy,
                x.ReviewedAt,
                x.CreatedAt,

                financial = _context.OrderFinancials
                    .Where(f => f.Id == x.OrderFinancialId)
                    .Select(f => new
                    {
                        f.OrderId,
                        f.CustomerPaid,
                        f.Currency,
                        f.SupplierAmount,
                        f.DriverAmount,
                        f.MechanicAmount,
                        f.AlphaPlatformFee,
                        f.FinancialStatus,
                        f.PayoutStatus
                    })
                    .FirstOrDefault()
            })
            .ToListAsync();

        return Ok(queue);
    }


    [HttpPost("{id}/approve")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Approve(Guid id)
    {
        var record = await _context.OrderFinancials.FindAsync(id);
        if (record == null) return NotFound();

        if (record.CustomerPaid <= 0)
            return BadRequest("Customer payment is not confirmed.");

        record.FinancialStatus = "approved";
        record.PayoutStatus = "approved_for_payout";

        if (record.SupplierAmount > 0)
        {
            _context.SettlementQueue.Add(new SettlementQueue
            {
                Id = Guid.NewGuid(),
                OrderFinancialId = record.Id,
                PayeeType = "supplier",
                Amount = record.SupplierAmount,
                Status = "pending_payout",
                CreatedAt = DateTime.UtcNow
            });
        }

        if (record.DriverAmount > 0)
        {
            _context.SettlementQueue.Add(new SettlementQueue
            {
                Id = Guid.NewGuid(),
                OrderFinancialId = record.Id,
                PayeeType = "driver",
                Amount = record.DriverAmount,
                Status = "pending_payout",
                CreatedAt = DateTime.UtcNow
            });
        }

        if (record.MechanicAmount > 0)
        {
            _context.SettlementQueue.Add(new SettlementQueue
            {
                Id = Guid.NewGuid(),
                OrderFinancialId = record.Id,
                PayeeType = "mechanic",
                Amount = record.MechanicAmount,
                Status = "pending_payout",
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();

        return Ok(record);
    }

    [HttpPost("{id}/hold")]
    [Authorize(Roles = "admin,dispatcher")]
    public async Task<IActionResult> Hold(Guid id)
    {
        var record = await _context.OrderFinancials.FindAsync(id);

        if (record == null)
            return NotFound();

        record.PayoutStatus = "on_hold";
        record.FinancialStatus = "needs_review";

        await _context.SaveChangesAsync();

        return Ok(record);
    }

    [HttpPost("settlement/{id}/mark-paid")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> MarkSettlementPaid(
    Guid id,
    CancellationToken cancellationToken)
    {
        var settlement = await _context.SettlementQueue
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        if (settlement == null)
            return NotFound();

        if (settlement.Status == "paid")
        {
            return Ok(new
            {
                success = true,
                message = "Settlement is already marked as paid.",
                settlementId = settlement.Id
            });
        }

        if (settlement.Status != "pending_payout" &&
            settlement.Status != "ready_for_payout")
        {
            return BadRequest(new
            {
                message =
                    $"Settlement cannot be paid from status '{settlement.Status}'."
            });
        }

        var financial =
            await _context.OrderFinancials
                .FirstOrDefaultAsync(
                    x => x.Id == settlement.OrderFinancialId,
                    cancellationToken);

        if (financial == null)
        {
            return NotFound(new
            {
                message = "Financial record not found."
            });
        }

        if (!financial.OrderId.HasValue)
        {
            return BadRequest(new
            {
                message = "Financial record has no order ID."
            });
        }

        var order =
            await _context.Orders
                .FirstOrDefaultAsync(
                    x => x.Id == financial.OrderId.Value,
                    cancellationToken);

        if (order == null)
        {
            return NotFound(new
            {
                message = "Order not found."
            });
        }

        // Mark this settlement as paid
        settlement.Status = "paid";
        settlement.ReviewedAt = DateTime.UtcNow;
        settlement.ReviewedBy =
            User.Identity?.Name ?? "admin";

        // Check whether all settlement rows
        // belonging to this order are now paid.
        var remainingSettlements =
            await _context.SettlementQueue
                .AnyAsync(
                    x =>
                        x.OrderFinancialId ==
                            settlement.OrderFinancialId
                        &&
                        x.Id != settlement.Id
                        &&
                        x.Status != "paid",
                    cancellationToken);

        if (!remainingSettlements)
        {
            financial.PayoutStatus = "paid";
            financial.SettlementStatus = "paid";
            financial.ProviderPayoutStatus = "paid";

            await _entrepreneurCommissionService
                .GenerateForOrderAfterSettlementPaidAsync(
                    order.Id,
                    cancellationToken);
        }


        await _entrepreneurCommissionService
    .GenerateForPaidSettlementAsync(
        settlement,
        order,
        financial,
        cancellationToken);
        // Only mark the complete financial settlement
        // as paid when every settlement row is paid.
        if (!remainingSettlements)
        {
            financial.PayoutStatus = "paid";
            financial.SettlementStatus = "paid";
            financial.ProviderPayoutStatus = "paid";
        }
        if (settlement.PayeeType == "supplier")
        {
            if (!settlement.PayeeId.HasValue)
            {
                return BadRequest(new
                {
                    message = "Supplier settlement has no supplier ID."
                });
            }

            var supplier = await _context.Suppliers
                .FirstOrDefaultAsync(
                    x => x.Id == settlement.PayeeId.Value,
                    cancellationToken);

            if (supplier == null)
            {
                return BadRequest(new
                {
                    message = "Supplier not found."
                });
            }

            var existingPayout =
                await _context.SupplierPayouts
                    .FirstOrDefaultAsync(
                        x =>
                            x.OrderId == order.Id &&
                            x.SupplierId == supplier.Id,
                        cancellationToken);

            if (existingPayout == null)
            {
                _context.SupplierPayouts.Add(
                    new SupplierPayout
                    {
                        Id = Guid.NewGuid(),

                        SupplierId = supplier.Id,

                        OrderId = order.Id,

                        Amount = settlement.Amount,

                        Currency = financial.Currency,

                        PayoutStatus = "paid",

                        PaidAt = DateTime.UtcNow,

                        CreatedAt = DateTime.UtcNow
                    }
                );
            }
        }

        if (settlement.PayeeType == "driver")
        {
            if (!settlement.PayeeId.HasValue)
            {
                return BadRequest(new
                {
                    message = "Driver settlement has no driver ID."
                });
            }

            var driver = await _context.Drivers
                .FirstOrDefaultAsync(
                    x => x.Id == settlement.PayeeId.Value,
                    cancellationToken);

            if (driver == null)
            {
                return BadRequest(new
                {
                    message = "Driver not found."
                });
            }

            var existingPayout =
                await _context.DriverPayouts
                    .FirstOrDefaultAsync(
                        x =>
                            x.OrderId == order.Id &&
                            x.DriverId == driver.Id,
                        cancellationToken);

            if (existingPayout == null)
            {
                _context.DriverPayouts.Add(
                    new DriverPayout
                    {
                        Id = Guid.NewGuid(),

                        DriverId = driver.Id,

                        OrderId = order.Id,

                        Amount = settlement.Amount,

                        Currency = financial.Currency,

                        PayoutStatus = "paid",

                        PaidAt = DateTime.UtcNow,

                        CreatedAt = DateTime.UtcNow
                    }
                );
            }
        }

        await _context.SaveChangesAsync(
            cancellationToken);

        // Generate entrepreneur commission ONLY
        // after every settlement for this order has
        // been paid.
        if (!remainingSettlements)
        {
            await _entrepreneurCommissionService
                .GenerateForOrderAsync(
                    order.Id,
                    cancellationToken);
        }

        return Ok(new
        {
            success = true,
            settlementId = settlement.Id,
            settlementStatus = settlement.Status,
            orderId = order.Id,
            allSettlementsPaid = !remainingSettlements
        });
    }

    [HttpGet("supplier/{supplierId}/earnings")]
    [Authorize(Roles = "admin,dispatcher,supplier,provider")]
    public async Task<IActionResult> GetSupplierEarnings(
     Guid supplierId,
     CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var role = User.GetRole()
            .Trim()
            .ToLowerInvariant();

        if (role == "supplier" || role == "provider")
        {
            var supplier = await _context.Suppliers
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.UserId == userId,
                    cancellationToken);

            if (supplier == null)
                return Forbid();

            if (supplier.Id != supplierId)
                return Forbid();
        }

        var payouts = await _context.SupplierPayouts
            .AsNoTracking()
            .Where(x => x.SupplierId == supplierId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.OrderId,
                x.SupplierId,
                x.Amount,
                x.Currency,
                x.PayoutStatus,
                x.PaidAt,
                x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            totalPaid = payouts
                .Where(x => x.PayoutStatus == "paid")
                .Sum(x => x.Amount),

            pending = payouts
                .Where(x => x.PayoutStatus != "paid")
                .Sum(x => x.Amount),

            items = payouts
        });
    }

    [HttpGet("driver/{driverId}/wallet")]
    [Authorize(Roles = "admin,dispatcher,driver")]
    public async Task<IActionResult> GetDriverWallet(Guid driverId)
    {
        var orderEarnings = await _context.OrderFinancials
            .Where(x => x.DriverAmount > 0)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.OrderId,
                x.ServiceRequestId,
                amount = x.DriverAmount,
                x.Currency,
                x.FinancialStatus,
                x.PayoutStatus,
                x.CreatedAt
            })
            .ToListAsync();

        var totalEarned = orderEarnings.Sum(x => x.amount);
        var pending = orderEarnings
            .Where(x => x.PayoutStatus != "paid")
            .Sum(x => x.amount);

        return Ok(new
        {
            totalEarned,
            pending,
            items = orderEarnings
        });
    }

    [HttpPost("orders/{orderId}/verify-settlement")]
    [Authorize(Roles = "admin,dispatcher")]
    public async Task<IActionResult> VerifySettlement(
    Guid orderId,
    CancellationToken cancellationToken)
    {
        try
        {
            var financial =
                await _settlementService.VerifySettlementAfterProof(
                    orderId);

            if (financial == null)
            {
                return NotFound(new
                {
                    message = "Financial record not found."
                });
            }

            return Ok(new
            {
                message = "Settlement verification completed.",
                orderId,
                financialStatus = financial.FinancialStatus,
                payoutStatus = financial.PayoutStatus,
                settlementStatus = financial.SettlementStatus,
                reconciliationDifference =
                    financial.ReconciliationDifference
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                message = ex.Message,
                orderId
            });
        }
    }

    [HttpGet("my-supplier-earnings")]
    [Authorize(Roles = "supplier,provider")]
    public async Task<IActionResult> GetMySupplierEarnings(
    CancellationToken cancellationToken)
    {
        Guid userId;

        try
        {
            userId = User.GetUserId();
        }
        catch
        {
            return Unauthorized();
        }

        var supplier = await _context.Suppliers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.UserId == userId,
                cancellationToken);

        if (supplier == null)
            return NotFound("Supplier profile not found.");

        var payouts = await _context.SupplierPayouts
            .AsNoTracking()
            .Where(x => x.SupplierId == supplier.Id)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.OrderId,
                x.Amount,
                x.Currency,
                x.PayoutStatus,
                x.PaidAt,
                x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            totalPaid = payouts
                .Where(x => x.PayoutStatus == "paid")
                .Sum(x => x.Amount),

            pending = payouts
                .Where(x => x.PayoutStatus != "paid")
                .Sum(x => x.Amount),

            items = payouts
        });
    }

    [HttpGet("my-driver-wallet")]
    [Authorize(Roles = "driver")]
    public async Task<IActionResult> GetMyDriverWallet(
    CancellationToken cancellationToken)
    {
        Guid userId;

        try
        {
            userId = User.GetUserId();
        }
        catch
        {
            return Unauthorized();
        }

        var driver = await _context.Drivers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.UserId == userId,
                cancellationToken);

        if (driver == null)
            return NotFound("Driver profile not found.");

        var payouts = await _context.DriverPayouts
            .AsNoTracking()
            .Where(x => x.DriverId == driver.Id)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            totalPaid = payouts
                .Where(x => x.PayoutStatus == "paid")
                .Sum(x => x.Amount),

            pending = payouts
                .Where(x => x.PayoutStatus != "paid")
                .Sum(x => x.Amount),

            items = payouts
        });
    }
}