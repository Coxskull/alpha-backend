using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Alpha.API.Data;

namespace Alpha.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DriversController : ControllerBase
{
    private readonly AppDbContext _context;

    public DriversController(AppDbContext context)
    {
        _context = context;
    }

    // =====================================================
    // GET ALL DRIVERS
    // =====================================================

    [HttpGet]
    public async Task<IActionResult> GetDrivers()
    {
        var drivers = await _context.Drivers
            .OrderBy(x => x.FullName)
            .ToListAsync();

        return Ok(drivers);
    }

    // =====================================================
    // GET DRIVER BY ID
    // =====================================================

    [HttpGet("{id}")]
    public async Task<IActionResult> GetDriver(Guid id)
    {
        var driver = await _context.Drivers
            .FirstOrDefaultAsync(x => x.Id == id);

        if (driver == null)
            return NotFound();

        return Ok(driver);
    }
}