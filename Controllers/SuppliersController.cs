using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Alpha.API.Data;

namespace Alpha.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SuppliersController : ControllerBase
{
    private readonly AppDbContext _context;

    public SuppliersController(AppDbContext context)
    {
        _context = context;
    }

    // =====================================================
    // GET ALL SUPPLIERS
    // =====================================================

    [HttpGet]
    public async Task<IActionResult> GetSuppliers()
    {
        var suppliers = await _context.Suppliers
            .OrderBy(x => x.Name)
            .ToListAsync();

        return Ok(suppliers);
    }

    // =====================================================
    // GET SUPPLIER BY ID
    // =====================================================

    [HttpGet("{id}")]
    public async Task<IActionResult> GetSupplier(Guid id)
    {
        var supplier = await _context.Suppliers
            .FirstOrDefaultAsync(x => x.Id == id);

        if (supplier == null)
            return NotFound();

        return Ok(supplier);
    }
    [HttpGet("available")]
    public async Task<IActionResult> GetAvailableSuppliers()
    {
        try
        {
            var suppliers = await _context.Suppliers
                .Where(x => x.AvailabilityStatus == "available")
                .Select(x => new
                {
                    x.Id,
                    Name = x.Name,
                    Availability = x.AvailabilityStatus,
                    Territory = x.Territory,
                    CurrentWorkload = x.CurrentWorkload
                })
                .ToListAsync();

            return Ok(suppliers);
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.ToString());
        }
    }

}