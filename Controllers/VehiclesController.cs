using Alpha.API.Data;
using Alpha.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace Alpha.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VehiclesController : ControllerBase
{
    private readonly AppDbContext _context;

    public VehiclesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("customer/{customerId:guid}")]
    public async Task<IActionResult> GetVehicles(Guid customerId)
    {
        var vehicles = await _context.CustomerVehicles
            .Where(x => x.CustomerId == customerId)
            .ToListAsync();

        return Ok(vehicles);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetVehicle(Guid id)
    {
        var vehicle = await _context.CustomerVehicles
            .FirstOrDefaultAsync(x => x.Id == id);

        if (vehicle == null)
            return NotFound();

        return Ok(vehicle);
    }

    [HttpPost]
    public async Task<IActionResult> AddVehicle(CustomerVehicle vehicle)
    {
        vehicle.Id = Guid.NewGuid();
        vehicle.CreatedAt = DateTime.UtcNow;

        _context.CustomerVehicles.Add(vehicle);

        await _context.SaveChangesAsync();

        return Ok(vehicle);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteVehicle(Guid id)
    {
        var vehicle = await _context.CustomerVehicles.FindAsync(id);

        if (vehicle == null)
            return NotFound();

        _context.CustomerVehicles.Remove(vehicle);

        await _context.SaveChangesAsync();

        return NoContent();
    }
}