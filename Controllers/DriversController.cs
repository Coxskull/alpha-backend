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

    [HttpGet("by-user/{userId}")]
    public async Task<IActionResult> GetDriverByUser(Guid userId)
    {
        var user = await _context.Users.FindAsync(userId);

        if (user == null)
            return NotFound("User not found.");

        var driver = await _context.Drivers
    .FirstOrDefaultAsync(d => d.Email == user.Email);

        if (driver == null)
            return NotFound("Driver profile not found for this user.");

        return Ok(new
        {
            id = driver.Id,
            fullName = driver.FullName,
            email = driver.Email,
            availabilityStatus = driver.AvailabilityStatus,
            territory = driver.Territory
        });
    }
}