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
    private readonly AlphaDbContext _context;

    public VehiclesController(AlphaDbContext context)
    {
        _context = context;
    }

    [HttpGet("customer/{customerId}")]
    public async Task<IActionResult> GetVehicles(Guid customerId)
    {
        var vehicles = await _context.CustomerVehicles
            .Where(x => x.CustomerId == customerId)
            .ToListAsync();

        return Ok(vehicles);
    }

    [HttpPost]
    public async Task<IActionResult> AddVehicle(CustomerVehicle vehicle)
    {
        vehicle.Id = Guid.NewGuid();

        _context.CustomerVehicles.Add(vehicle);

        await _context.SaveChangesAsync();

        return Ok(vehicle);
    }
}