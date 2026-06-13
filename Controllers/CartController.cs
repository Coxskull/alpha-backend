using Alpha.API.Data;
using Alpha.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace Alpha.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CartController : ControllerBase
{
    private readonly AlphaDbContext _context;

    public CartController(AlphaDbContext context)
    {
        _context = context;
    }

    [HttpGet("{customerId}")]
    public async Task<IActionResult> GetCart(Guid customerId)
    {
        var items = await _context.CartItems
            .Where(x => x.CustomerId == customerId)
            .ToListAsync();

        return Ok(items);
    }

    [HttpPost("add")]
    public async Task<IActionResult> AddToCart(CartItem item)
    {
        item.Id = Guid.NewGuid();

        _context.CartItems.Add(item);

        await _context.SaveChangesAsync();

        return Ok(item);
    }

    [HttpDelete("remove/{id}")]
    public async Task<IActionResult> Remove(Guid id)
    {
        var item = await _context.CartItems.FindAsync(id);

        if (item == null)
            return NotFound();

        _context.CartItems.Remove(item);

        await _context.SaveChangesAsync();

        return NoContent();
    }
}