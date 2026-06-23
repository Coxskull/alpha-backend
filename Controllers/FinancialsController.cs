using Alpha.API.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace Alpha.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "admin,dispatcher")]
public class FinancialsController : ControllerBase
{
    private readonly AppDbContext _context;

    public FinancialsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("settlement-queue")]
    public async Task<IActionResult> GetSettlementQueue()
    {
        return Ok(await _context.OrderFinancials
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync());
    }

    [HttpPost("{id}/approve")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Approve(Guid id)
    {
        var record = await _context.OrderFinancials.FindAsync(id);

        if (record == null)
            return NotFound();

        record.PayoutStatus = "approved_for_payout";
        record.FinancialStatus = "approved";

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
}