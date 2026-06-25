using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Alpha.API.Data;
using Microsoft.AspNetCore.Authorization;
namespace Alpha.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
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
    [HttpGet("available")]
    public async Task<IActionResult> GetAvailableDrivers()
    {
        try
        {
            var drivers = await _context.Drivers
                .Where(x => x.AvailabilityStatus == "available")
                .Select(x => new
                {
                    x.Id,
                    x.FullName,
                    Availability = x.AvailabilityStatus,
                    Territory = x.Territory,
                    ActiveJobs = x.ActiveJobs
                })
                .ToListAsync();

            return Ok(drivers);
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.ToString());
        }
    }
}