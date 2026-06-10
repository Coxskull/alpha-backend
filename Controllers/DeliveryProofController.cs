using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Alpha.API.Data;

namespace Alpha.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DeliveryProofController : ControllerBase
{
    private readonly AppDbContext _context;

    public DeliveryProofController(AppDbContext context)
    {
        _context = context;
    }

    // =====================================================
    // GET DELIVERY PROOF
    // =====================================================

    [HttpGet("{orderId}")]
    public async Task<IActionResult> GetProof(Guid orderId)
    {
        var proof = await _context.DeliveryProofs
            .Where(x => x.OrderId == orderId)
            .OrderByDescending(x => x.UploadedAt)
            .FirstOrDefaultAsync();

        if (proof == null)
            return NotFound();

        return Ok(proof);
    }
}