using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Alpha.API.Data;

namespace Alpha.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _context;

    public DashboardController(AppDbContext context)
    {
        _context = context;
    }

    // =====================================================
    // DASHBOARD STATS
    // =====================================================

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var orders =
            await _context.Orders.ToListAsync();

        var suppliers =
            await _context.Suppliers.ToListAsync();

        var drivers =
            await _context.Drivers.ToListAsync();

        var response = new
        {
            activeOrders = orders.Count,

            availableDrivers =
                drivers.Count(x =>
                    x.AvailabilityStatus == "available"),

            activeSuppliers =
                suppliers.Count(x =>
                    x.AvailabilityStatus == "available"),

            enRouteDeliveries =
                orders.Count(x =>
                    x.Status == "en_route"),

            deliveredToday =
                orders.Count(x =>
                    x.Status == "delivered")
        };

        return Ok(response);
    }
}