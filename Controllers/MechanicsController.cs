using Alpha.API.Data;
using Alpha.API.DTOs;
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
public class MechanicsController : ControllerBase
{
    private readonly AppDbContext _context;

    public MechanicsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [Authorize(Roles = "admin,dispatcher")]
    public async Task<IActionResult> GetMechanics()
    {
        return Ok(await _context.Mechanics.ToListAsync());
    }

    [HttpGet("available")]
    [Authorize(Roles = "admin,dispatcher")]
    public async Task<IActionResult> GetAvailableMechanics()
    {
        var mechanics = await _context.Mechanics
            .Where(m => m.AvailabilityStatus == "available")
            .ToListAsync();

        return Ok(mechanics);
    }

    [HttpGet("me")]
    [Authorize(Roles = "mechanic")]
    public async Task<IActionResult> Me()
    {
        var userId = User.GetUserId();

        var mechanic = await _context.Mechanics
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (mechanic == null)
            return NotFound("Mechanic profile not found.");

        return Ok(mechanic);
    }

    [HttpPost("me/availability")]
    [Authorize(Roles = "mechanic")]
    public async Task<IActionResult> UpdateMyAvailability(UpdateAvailabilityDto dto)
    {
        var allowed = new[] { "available", "busy", "offline" };

        if (!allowed.Contains(dto.Status))
            return BadRequest("Invalid availability status.");

        var userId = User.GetUserId();

        var mechanic = await _context.Mechanics
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (mechanic == null)
            return Forbid();

        mechanic.AvailabilityStatus = dto.Status;

        await _context.SaveChangesAsync();

        return Ok(mechanic);
    }

    [HttpPost("{id}/availability")]
    [Authorize(Roles = "admin,dispatcher")]
    public async Task<IActionResult> UpdateAvailability(Guid id, UpdateAvailabilityDto dto)
    {
        var mechanic = await _context.Mechanics.FindAsync(id);

        if (mechanic == null)
            return NotFound();

        mechanic.AvailabilityStatus = dto.Status;

        await _context.SaveChangesAsync();

        return Ok(mechanic);
    }
}