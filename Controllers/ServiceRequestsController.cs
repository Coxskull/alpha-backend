using Alpha.API.Data;
using Alpha.API.DTOs;
using Alpha.API.Models;
using Alpha.API.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Alpha.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ServiceRequestsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ServiceRequestsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [Authorize(Roles = "admin,dispatcher")]
    public async Task<IActionResult> GetAll()
    {
        var requests = await _context.ServiceRequests
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(requests);
    }

    [HttpGet("my")]
    [Authorize(Roles = "mechanic")]
    public async Task<IActionResult> GetMyRequests()
    {
        var userId = User.GetUserId();

        var mechanic = await _context.Mechanics
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (mechanic == null)
            return Forbid();

        var requests = await _context.ServiceRequests
            .Where(x => x.MechanicId == mechanic.Id)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(requests);
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Create(ServiceRequest request)
    {
        request.Id = Guid.NewGuid();
        request.Status = "new_request";
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
        var request = await GetOwnedMechanicRequest(id);

        if (request == null)
            return Forbid();

        request.Status = "accepted";
        request.AcceptedAt = DateTime.UtcNow;
        request.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(request);
    }

    [HttpPost("{id}/reject")]
    [Authorize(Roles = "mechanic")]
    public async Task<IActionResult> Reject(Guid id, RejectServiceRequestDto dto)
    {
        var request = await GetOwnedMechanicRequest(id);

        if (request == null)
            return Forbid();

        request.Status = "rejected";
        request.RejectionReason = dto.Reason;
        request.MechanicId = null;
        request.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(request);
    }

    [HttpPost("{id}/status")]
    [Authorize(Roles = "mechanic")]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateServiceStatusDto dto)
    {
        var allowedStatuses = new[]
        {
            "accepted",
            "en_route",
            "started",
            "waiting_for_parts",
            "completed",
            "closed"
        };

        if (!allowedStatuses.Contains(dto.Status))
            return BadRequest("Invalid mechanic job status.");

        var request = await GetOwnedMechanicRequest(id);

        if (request == null)
            return Forbid();

        request.Status = dto.Status;
        request.UpdatedAt = DateTime.UtcNow;

        if (dto.Status == "started")
            request.StartedAt = DateTime.UtcNow;

        if (dto.Status == "completed")
            request.CompletedAt = DateTime.UtcNow;

        if (dto.Status == "closed")
            request.ClosedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(request);
    }

    [HttpPost("{id}/request-parts")]
    [Authorize(Roles = "mechanic")]
    public async Task<IActionResult> RequestParts(Guid id, RequestPartsDto dto)
    {
        var request = await GetOwnedMechanicRequest(id);

        if (request == null)
            return Forbid();

        request.Status = "waiting_for_parts";
        request.PartsRequestNote = dto.Notes;
        request.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(request);
    }

    [HttpPost("{id}/proof")]
    [Authorize(Roles = "mechanic")]
    public async Task<IActionResult> UploadProof(Guid id, UploadProofDto dto)
    {
        var request = await GetOwnedMechanicRequest(id);

        if (request == null)
            return Forbid();

        request.ProofImageUrl = dto.ImageUrl;
        request.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(request);
    }

    [HttpPost("{id}/complete")]
    [Authorize(Roles = "mechanic")]
    public async Task<IActionResult> Complete(Guid id)
    {
        var request = await GetOwnedMechanicRequest(id);

        if (request == null)
            return Forbid();

        request.Status = "completed";
        request.CompletedAt = DateTime.UtcNow;
        request.UpdatedAt = DateTime.UtcNow;

        var mechanic = await _context.Mechanics
            .FirstOrDefaultAsync(x => x.Id == request.MechanicId);

        if (mechanic != null)
        {
            mechanic.ActiveJobs = Math.Max(0, mechanic.ActiveJobs - 1);
            mechanic.AvailabilityStatus = "available";
        }

        await _context.SaveChangesAsync();

        return Ok(request);
    }

    private async Task<ServiceRequest?> GetOwnedMechanicRequest(Guid requestId)
    {
        var userId = User.GetUserId();

        var mechanic = await _context.Mechanics
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (mechanic == null)
            return null;

        return await _context.ServiceRequests
            .FirstOrDefaultAsync(x =>
                x.Id == requestId &&
                x.MechanicId == mechanic.Id);
    }
}