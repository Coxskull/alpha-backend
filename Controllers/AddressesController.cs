using Alpha.API.Data;
using Alpha.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace Alpha.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AddressesController : ControllerBase
{
    private readonly AppDbContext _context;

    public AddressesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("{customerId:guid}")]
    public async Task<IActionResult> GetAddresses(Guid customerId)
    {
        var addresses = await _context.CustomerAddresses
            .Where(x => x.CustomerId == customerId)
            .ToListAsync();

        return Ok(addresses);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAddress(CustomerAddress address)
    {
        address.Id = Guid.NewGuid();
        address.CreatedAt = DateTime.UtcNow;

        _context.CustomerAddresses.Add(address);

        await _context.SaveChangesAsync();

        return Ok(address);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAddress(Guid id)
    {
        var address = await _context.CustomerAddresses.FindAsync(id);

        if (address == null)
            return NotFound();

        _context.CustomerAddresses.Remove(address);

        await _context.SaveChangesAsync();

        return NoContent();
    }
}