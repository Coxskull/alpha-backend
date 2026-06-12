using System.Threading.Tasks;

[ApiController]
[Route("api/[controller]")]
public class AlertsController
    : ControllerBase
{
    private readonly AppDbContext _context;

    public AlertsController(
        AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var alerts =
            await _context
                .OperationalAlerts
                .Where(x => !x.Resolved)
                .OrderByDescending(
                    x => x.CreatedAt)
                .ToListAsync();

        return Ok(alerts);
    }
}