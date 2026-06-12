using Alpha.API.Data;
using Alpha.API.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace Alpha.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AlertsController : ControllerBase
{
    private readonly AppDbContext _context;

    public AlertsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAlerts()
    {
        var alerts =
            await AlertService.GenerateAlerts(_context);

        return Ok(alerts);
    }
}