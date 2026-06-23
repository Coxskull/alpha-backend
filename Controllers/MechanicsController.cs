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
    public async Task<IActionResult> GetAvailableMechanics()
    {
        var mechanics = await _context.Mechanics
            .Where(m => m.AvailabilityStatus == "available")
            .ToListAsync();

        return Ok(mechanics);
    }

    [HttpPost("{id}/availability")]
    [Authorize(Roles = "mechanic,admin,dispatcher")]
    public async Task<IActionResult> UpdateAvailability(
        Guid id,
        [FromBody] string status)
    {
        var mechanic = await _context.Mechanics.FindAsync(id);

        if (mechanic == null)
            return NotFound();

        mechanic.AvailabilityStatus = status;

        await _context.SaveChangesAsync();

        return Ok(mechanic);
    }
}