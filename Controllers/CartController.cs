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
    private readonly AppDbContext _context;

    public CartController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("{customerId:guid}")]
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
        item.CreatedAt = DateTime.UtcNow;

        _context.CartItems.Add(item);

        await _context.SaveChangesAsync();

        return Ok(item);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateQuantity(
        Guid id,
        [FromBody] int quantity)
    {
        var item = await _context.CartItems.FindAsync(id);

        if (item == null)
            return NotFound();

        item.Quantity = quantity;

        await _context.SaveChangesAsync();

        return Ok(item);
    }

    [HttpDelete("remove/{id:guid}")]
    public async Task<IActionResult> Remove(Guid id)
    {
        var item = await _context.CartItems.FindAsync(id);

        if (item == null)
            return NotFound();

        _context.CartItems.Remove(item);

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("clear/{customerId:guid}")]
    public async Task<IActionResult> ClearCart(Guid customerId)
    {
        var items = await _context.CartItems
            .Where(x => x.CustomerId == customerId)
            .ToListAsync();

        _context.CartItems.RemoveRange(items);

        await _context.SaveChangesAsync();

        return NoContent();
    }
}