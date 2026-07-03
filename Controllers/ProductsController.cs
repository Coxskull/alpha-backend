using Alpha.API.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using Alpha.API.DTOs;
using Alpha.API.Models;

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
        var products = await _context.Products
    .Where(x => x.IsActive && x.QuantityAvailable > 0)
    .OrderByDescending(x => x.CreatedAt)
    .ToListAsync();

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

    [HttpPost]
    public async Task<IActionResult> CreateProduct(CreateProductDto dto)
    {
        var supplier = await _context.Suppliers.FindAsync(dto.SupplierId);

        if (supplier == null)
            return NotFound("Supplier not found.");

        var product = new Product
        {
            Id = Guid.NewGuid(),
            SupplierId = dto.SupplierId,
            PartNumber = dto.PartNumber,
            Brand = dto.Brand,
            Name = dto.Name,
            Description = dto.Description,
            ImageUrl = dto.ImageUrl,
            Price = dto.Price,
            QuantityAvailable = dto.QuantityAvailable,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Products.Add(product);

        await _context.SaveChangesAsync();

        return Ok(product);
    }

    [HttpGet("supplier/{supplierId}")]
    public async Task<IActionResult> GetSupplierProducts(Guid supplierId)
    {
        var products = await _context.Products
            .Where(x => x.SupplierId == supplierId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(products);
    }
}