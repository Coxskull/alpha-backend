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
    private readonly AlphaDbContext _context;

    public AddressesController(AlphaDbContext context)
    {
        _context = context;
    }

    [HttpGet("{customerId}")]
    public async Task<IActionResult> GetAddresses(Guid customerId)
    {
        var addresses = await _context.CustomerAddresses
            .Where(x => x.CustomerId == customerId)
            .ToListAsync();

        return Ok(addresses);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CustomerAddress address)
    {
        address.Id = Guid.NewGuid();

        _context.CustomerAddresses.Add(address);

        await _context.SaveChangesAsync();

        return Ok(address);
    }
}