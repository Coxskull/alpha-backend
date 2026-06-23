using Alpha.API.Data;
using Alpha.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace Alpha.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ServiceRequestsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ServiceRequestsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [Authorize(Roles = "admin,dispatcher,mechanic")]
    public async Task<IActionResult> GetRequests()
    {
        return Ok(await _context.ServiceRequests
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> Create(ServiceRequest request)
    {
        request.Id = Guid.NewGuid();
        request.Status = "pending";
        request.CreatedAt = DateTime.UtcNow;
        request.UpdatedAt = DateTime.UtcNow;

        _context.ServiceRequests.Add(request);
        await _context.SaveChangesAsync();

        return Ok(request);
    }

    [HttpPost("{id}/assign-nearest-mechanic")]
    [Authorize(Roles = "admin,dispatcher")]
    public async Task<IActionResult> AssignNearestMechanic(Guid id)
    {
        var request = await _context.ServiceRequests.FindAsync(id);

        if (request == null)
            return NotFound();

        var mechanic = await _context.Mechanics
            .Where(m =>
                m.AvailabilityStatus == "available" &&
                m.ServiceArea == request.Zone)
            .OrderBy(m => m.ActiveJobs)
            .ThenByDescending(m => m.ResponseRate)
            .FirstOrDefaultAsync();

        if (mechanic == null)
        {
            request.Status = "mechanic_needed";
            request.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return BadRequest("No available mechanic found.");
        }

        request.MechanicId = mechanic.Id;
        request.Status = "mechanic_assigned";
        request.UpdatedAt = DateTime.UtcNow;

        mechanic.AvailabilityStatus = "busy";
        mechanic.ActiveJobs += 1;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            request,
            mechanic
        });
    }

    [HttpPost("{id}/accept")]
    [Authorize(Roles = "mechanic")]
    public async Task<IActionResult> Accept(Guid id)
    {
        var request = await _context.ServiceRequests.FindAsync(id);

        if (request == null)
            return NotFound();

        request.Status = "mechanic_accepted";
        request.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(request);
    }

    [HttpPost("{id}/reject")]
    [Authorize(Roles = "mechanic")]
    public async Task<IActionResult> Reject(Guid id)
    {
        var request = await _context.ServiceRequests.FindAsync(id);

        if (request == null)
            return NotFound();

        request.MechanicId = null;
        request.Status = "mechanic_rejected";
        request.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(request);
    }

    [HttpPost("{id}/in-progress")]
    [Authorize(Roles = "mechanic")]
    public async Task<IActionResult> InProgress(Guid id)
    {
        var request = await _context.ServiceRequests.FindAsync(id);

        if (request == null)
            return NotFound();

        request.Status = "repair_in_progress";
        request.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(request);
    }

    [HttpPost("{id}/complete")]
    [Authorize(Roles = "mechanic")]
    public async Task<IActionResult> Complete(Guid id)
    {
        var request = await _context.ServiceRequests.FindAsync(id);

        if (request == null)
            return NotFound();

        request.Status = "repair_completed";
        request.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var financial = new OrderFinancial
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = request.Id,
            CustomerPaid = 0,
            MechanicAmount = 0,
            AlphaPlatformFee = 0,
            FinancialStatus = "pending_review",
            PayoutStatus = "manual_review",
            CreatedAt = DateTime.UtcNow
        };

        _context.OrderFinancials.Add(financial);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            request,
            financial
        });
    }
}