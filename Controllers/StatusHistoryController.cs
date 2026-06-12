using Alpha.API.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Alpha.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StatusHistoryController : ControllerBase
{
    private readonly AppDbContext _context;

    public StatusHistoryController(
        AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("{orderId}")]
    public async Task<IActionResult> GetTimeline(
        Guid orderId)
    {
        var history = await _context.StatusHistory
            .Where(x => x.OrderId == orderId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();

        return Ok(history);
    }
}