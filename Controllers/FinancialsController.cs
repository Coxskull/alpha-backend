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
    public async Task<IActionResult> MarkSettlementPaid(Guid id)
    {
        var settlement = await _context.SettlementQueue.FindAsync(id);
        if (settlement == null) return NotFound();

        settlement.Status = "paid";
        settlement.ReviewedAt = DateTime.UtcNow;
        settlement.ReviewedBy = User.Identity?.Name ?? "admin";

        await _context.SaveChangesAsync();

        return Ok(settlement);
    }

    [HttpGet("supplier/{supplierId}/earnings")]
    [Authorize(Roles = "admin,dispatcher,supplier,provider")]
    public async Task<IActionResult> GetSupplierEarnings(Guid supplierId)
    {
        var records = await _context.OrderFinancials
            .Where(x => x.SupplierAmount > 0)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.OrderId,
                x.ServiceRequestId,
                amount = x.SupplierAmount,
                x.Currency,
                x.FinancialStatus,
                x.PayoutStatus,
                x.CreatedAt
            })
            .ToListAsync();

        return Ok(records);
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
}