using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Alpha.API.Data;

namespace Alpha.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuditLogsController : ControllerBase
{
    private readonly AppDbContext _context;

    public AuditLogsController(AppDbContext context)
    {
        _context = context;
    }

    // =====================================================
    // GET ORDER AUDIT LOGS
    // =====================================================

    [HttpGet("{orderId}")]
    public async Task<IActionResult> GetLogs(Guid orderId)
    {
        var logs = await _context.AuditLogs
            .Where(x => x.OrderId == orderId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(logs);
    }
}