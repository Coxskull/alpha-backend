using Microsoft.AspNetCore.Authorization;
using Alpha.API.Security;
using Alpha.API.Data;
using Alpha.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace Alpha.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly AppDbContext _context;

    public CustomersController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [Authorize(Roles = "admin,dispatcher")]
    public async Task<IActionResult> GetCustomers()
    {
        return Ok(await _context.Customers.ToListAsync());
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetCustomer(Guid id)
    {
        if (!OrderAccess.CanAccessCustomer(User, id)) return Forbid();
        var customer = await _context.Customers.FindAsync(id);

        if (customer == null)
            return NotFound();

        return Ok(customer);
    }

    [HttpPost]
    [Authorize(Roles = "admin,dispatcher")]
    public async Task<IActionResult> CreateCustomer(Customer customer)
    {
        customer.Id = Guid.NewGuid();
        customer.CreatedAt = DateTime.UtcNow;

        _context.Customers.Add(customer);

        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetCustomer),
            new { id = customer.Id },
            customer);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "admin,dispatcher")]
    public async Task<IActionResult> DeleteCustomer(Guid id)
    {
        var customer = await _context.Customers.FindAsync(id);

        if (customer == null)
            return NotFound();

        _context.Customers.Remove(customer);

        await _context.SaveChangesAsync();

        return NoContent();
    }
}