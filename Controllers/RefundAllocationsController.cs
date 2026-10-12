using System.Security.Claims;
using Alpha.API.Data;
using Alpha.API.DTOs;
using Alpha.API.Models;
using Alpha.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Alpha.API.Controllers;

[ApiController]
[Route("api/refund-allocations")]
[Authorize(Roles = "admin")]
public sealed class RefundAllocationsController(AppDbContext db) : ControllerBase
{
    [HttpPost("{operationId:guid}")]
    public async Task<IActionResult> Allocate(Guid operationId, RefundAllocationDto plan, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var reviewer)) return Unauthorized();
        try { await new RefundAllocationService(db).AllocateAsync(operationId, plan, reviewer, ct); return Ok(new { operationId, status = "allocated_review", bankDebits = false }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }
    [HttpGet("{operationId:guid}")]
    public async Task<IActionResult> Get(Guid operationId, CancellationToken ct)
        => Ok(await db.Set<RefundAllocation>().AsNoTracking().Where(a => a.OperationId == operationId).ToListAsync(ct));
    [HttpGet("recovery-balances")]
    public async Task<IActionResult> Balances(CancellationToken ct)
        => Ok(await db.Set<RefundAllocation>().AsNoTracking().Where(a => a.FutureRecovery).GroupBy(a => new { a.PayeeType, a.PayeeId, a.Currency })
            .Select(g => new { g.Key.PayeeType, g.Key.PayeeId, g.Key.Currency, outstanding = g.Sum(a => a.Amount - a.RecoveredAmount) }).ToListAsync(ct));
}
