using Alpha.API.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace Alpha.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ProductsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetProducts()
    {
        try
        {
            var products = await _context.Products.ToListAsync();
            return Ok(products);
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.ToString());
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetProduct(Guid id)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(x => x.Id == id);

        if (product == null)
            return NotFound();

        return Ok(product);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return BadRequest("Keyword is required.");

        var products = await _context.Products
            .Where(x =>
                x.Name.Contains(keyword) ||
                x.Brand.Contains(keyword))
            .ToListAsync();

        return Ok(products);
    }
}