using Alpha.API.Data;
using Alpha.API.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Alpha.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _context;

    public DashboardController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("overview")]
    [Authorize(Roles = "admin,dispatcher")]
    public async Task<IActionResult> Overview()
    {
        var today = DateTime.UtcNow.Date;

        var serviceRequests = await _context.ServiceRequests.ToListAsync();
        var financials = await _context.OrderFinancials.ToListAsync();

        return Ok(new
        {
            serviceRequests = new
            {
                total = serviceRequests.Count,
                active = serviceRequests.Count(x =>
                    x.Status != "completed" &&
                    x.Status != "closed" &&
                    x.Status != "cancelled"),
                newRequests = serviceRequests.Count(x => x.Status == "new_request"),
                providerNeeded = serviceRequests.Count(x => x.Status == "provider_needed"),
                mechanicNeeded = serviceRequests.Count(x => x.Status == "mechanic_needed"),
                driverNeeded = serviceRequests.Count(x => x.Status == "driver_needed"),
                waitingForParts = serviceRequests.Count(x => x.Status == "parts_requested"),
                completedToday = serviceRequests.Count(x =>
                    x.Status == "completed" &&
                    x.CompletedAt != null &&
                    x.CompletedAt.Value.Date == today)
            },
            financials = new
            {
                grossRevenue = financials.Sum(x => x.TotalAmount),
                customerPaid = financials.Sum(x => x.CustomerPaid),
                alphaRevenue = financials.Sum(x => x.AlphaPlatformFee),
                providerPayouts = financials.Sum(x => x.SupplierAmount),
                mechanicPayouts = financials.Sum(x => x.MechanicAmount),
                driverPayouts = financials.Sum(x => x.DriverAmount),
                pendingReview = financials.Count(x => x.FinancialStatus == "pending_review")
            }
        });
    }

    [HttpGet("mechanic")]
    [Authorize(Roles = "mechanic")]
    public async Task<IActionResult> MechanicDashboard()
    {
        Guid userId;

        try
        {
            userId = User.GetUserId();
        }
        catch
        {
            return Forbid();
        }

        var mechanic = await _context.Mechanics
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (mechanic == null)
            return Forbid();

        var requests = await _context.ServiceRequests
            .Where(x => x.MechanicId == mechanic.Id)
            .ToListAsync();

        var requestIds = requests.Select(x => x.Id).ToList();

        var financials = await _context.OrderFinancials
            .Where(x =>
                x.ServiceRequestId != null &&
                requestIds.Contains(x.ServiceRequestId.Value))
            .ToListAsync();

        return Ok(new
        {
            jobs = new
            {
                assigned = requests.Count(x => x.Status == "mechanic_assigned"),
                accepted = requests.Count(x => x.Status == "mechanic_accepted"),
                waitingForParts = requests.Count(x => x.Status == "parts_requested"),
                completed = requests.Count(x => x.Status == "completed")
            },
            financials = new
            {
                earnings = financials.Sum(x => x.MechanicAmount),
                pendingReview = financials.Count(x => x.FinancialStatus == "pending_review")
            }
        });
    }

    [HttpGet("driver")]
    [Authorize(Roles = "driver,Driver,admin,dispatcher")]
    public async Task<IActionResult> DriverDashboard()
    {
        string email;

        try
        {
            email = User.GetEmail();
        }
        catch
        {
            return Forbid();
        }

        var driver = await _context.Drivers
            .FirstOrDefaultAsync(x => x.Email == email);

        if (driver == null)
            return Forbid("No driver profile is linked to this user email.");

        var requests = await _context.ServiceRequests
            .Where(x => x.DriverId == driver.Id)
            .ToListAsync();

        var requestIds = requests.Select(x => x.Id).ToList();

        var financials = await _context.OrderFinancials
            .Where(x =>
                x.ServiceRequestId != null &&
                requestIds.Contains(x.ServiceRequestId.Value))
            .ToListAsync();

        return Ok(new
        {
            jobs = new
            {
                assigned = requests.Count(x => x.Status == "driver_assigned"),
                pickedUp = requests.Count(x => x.Status == "parts_picked_up"),
                delivered = requests.Count(x => x.Status == "parts_delivered"),
                completed = requests.Count(x => x.Status == "completed")
            },
            financials = new
            {
                earnings = financials.Sum(x => x.DriverAmount)
            }
        });
    }
}