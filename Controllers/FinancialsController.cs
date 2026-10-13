using OrderAccess = Alpha.API.Security.OrderAccess;
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
using Alpha.API.Extensions;
using Alpha.API.Constants;

namespace Alpha.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
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
    [Authorize(Roles = "admin,dispatcher")]
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
                x.RefundAdjustment,
                x.RecoveryOffset,
                netRecorded = x.Amount - x.RefundAdjustment - x.RecoveryOffset,
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

    [HttpGet("pending-settlements")]
    [Authorize(Roles = "admin,dispatcher")]
    public async Task<IActionResult> GetPendingSettlements(
    CancellationToken cancellationToken)
    {
        var pending = await _context.Orders
            .Where(x =>
                x.Status == OrderStatuses.SettlementPending)
            .Select(order => new
            {
                orderId = order.Id,
                orderNumber = order.OrderNumber,
                customerName = order.CustomerName,
                pickupAddress = order.PickupAddress,
                deliveryAddress = order.DeliveryAddress,
                supplierId = order.SupplierId,
                driverId = order.DriverId,
                status = order.Status,
                currency = order.Currency,
                countryCode = order.CountryCode,
                createdAt = order.CreatedAt,
                updatedAt = order.UpdatedAt,

                financial =
                    _context.OrderFinancials
                        .Where(f =>
                            f.OrderId == order.Id)
                        .Select(f => new
                        {
                            id = f.Id,
                            customerPaid = f.CustomerPaid,
                            currency = f.Currency,

                            supplierAmount =
                                f.SupplierAmount,

                            driverAmount =
                                f.DriverAmount,

                            mechanicAmount =
                                f.MechanicAmount,

                            supplierNetPayable =
                                f.SupplierNetPayable,

                            driverNetPayable =
                                f.DriverNetPayable,

                            mechanicNetPayable =
                                f.MechanicNetPayable,

                            tax = f.Tax,

                            taxCollected =
                                f.TaxCollected,

                            taxWithheld =
                                f.TaxWithheld,

                            processingFee =
                                f.ProcessingFee,

                            alphaNetRevenue =
                                f.AlphaNetRevenue,

                            financialStatus =
                                f.FinancialStatus,

                            settlementStatus =
                                f.SettlementStatus,

                            payoutStatus =
                                f.PayoutStatus,

                            reconciliationDifference =
                                f.ReconciliationDifference,

                            completionProofUrl =
                                f.CompletionProofUrl
                        })
                        .FirstOrDefault(),

                proof =
                    _context.DeliveryProofs
                        .Where(p =>
                            p.OrderId == order.Id)
                        .OrderByDescending(p =>
                            p.UploadedAt)
                        .Select(p => new
                        {
                            id = p.Id,
                            imageUrl = p.ImageUrl,
                            uploadedAt = p.UploadedAt
                        })
                        .FirstOrDefault()
            })
            .OrderByDescending(x => x.updatedAt)
            .ToListAsync(cancellationToken);

        return Ok(pending);
    }


    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Approve(
     Guid id,
     CancellationToken cancellationToken)
    {
        var financial = await _context.OrderFinancials
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        if (financial == null)
            return NotFound();

        if (financial.FinancialStatus != "verified")
        {
            return BadRequest(new
            {
                message = "Financial record must be verified before approval."
            });
        }

        financial.PayoutStatus = "ready_for_payout";
        financial.SettlementStatus = "ready_for_payout";

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            financialId = financial.Id,
            status = financial.PayoutStatus
        });
    }

    [HttpPost("{id}/hold")]
    [Authorize(Roles = "admin,dispatcher")]
    public async Task<IActionResult> Hold(Guid id)
    {
        var record = await _context.OrderFinancials.FindAsync(id);

        if (record == null)
            return NotFound();

        record.PayoutStatus = "on_hold";
        if (record.FinancialStatus != RefundConflictGuard.Status)
            record.FinancialStatus = await RefundConflictGuard.HasAsync(_context, record.OrderId, CancellationToken.None) ? RefundConflictGuard.Status : "needs_review";

        await _context.SaveChangesAsync();

        return Ok(record);
    }

    [HttpGet("settlement/{id:guid}/payout-quote")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> SettlementQuote(Guid id, CancellationToken ct)
    {
        try { return Ok(await new PayoutAccountingService(_context).SettlementAsync(id, null, Guid.Empty, null, ct)); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpPost("settlement/{id:guid}/mark-paid")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> MarkSettlementPaid(Guid id, [FromBody] Alpha.API.DTOs.PayoutConfirmationDto confirmation, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var admin)) return Unauthorized();
        try { return Ok(await new PayoutAccountingService(_context).SettlementAsync(id, confirmation, admin, _entrepreneurCommissionService, ct)); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpGet("supplier/{supplierId}/earnings")]
    [Authorize(Roles = "admin,dispatcher,supplier,provider")]
    public async Task<IActionResult> GetSupplierEarnings(
     Guid supplierId,
     CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var staff = OrderAccess.IsStaff(User);
        if (!staff && !User.IsInRole("supplier") && !User.IsInRole("provider")) return Forbid();
        if (!staff)
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
        if (!OrderAccess.IsStaff(User) && !await _context.Drivers.AnyAsync(
            d => d.Id == driverId && d.UserId == User.GetUserId())) return Forbid();

        var orderEarnings = await _context.OrderFinancials
            .Where(x => x.DriverAmount > 0 && _context.Orders.Any(o => o.Id == x.OrderId && o.DriverId == driverId))
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
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> VerifySettlement(
    Guid orderId,
    CancellationToken cancellationToken)
    {
        try
        {
            var financial =
                await _settlementService.VerifySettlement(
                    orderId,
                    cancellationToken);

            if (financial.SettlementStatus == "blocked")
            {
                return BadRequest(new
                {
                    success = false,
                    message =
                        "Settlement was blocked during verification.",
                    financialStatus =
                        financial.FinancialStatus,
                    settlementStatus =
                        financial.SettlementStatus,
                    payoutStatus =
                        financial.PayoutStatus,
                    reconciliationDifference =
                        financial.ReconciliationDifference
                });
            }

            return Ok(new
            {
                success = true,
                message =
                    "Settlement verified and added to payout queue.",
                financialId =
                    financial.Id,
                orderId =
                    financial.OrderId,
                financialStatus =
                    financial.FinancialStatus,
                settlementStatus =
                    financial.SettlementStatus,
                payoutStatus =
                    financial.PayoutStatus,
                reconciliationDifference =
                    financial.ReconciliationDifference
            });
        }
        catch (InvalidOperationException)
        {
            return BadRequest(new
            {
                success = false,
                message = "The request could not be completed."
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
