using Alpha.API.Data;
using Alpha.API.DTOs;
using Alpha.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
        var products = await _context.Products
            .Where(x => x.IsActive && x.QuantityAvailable > 0)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(products);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetProduct(Guid id)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.IsActive &&
                x.QuantityAvailable > 0);

        if (product == null)
            return NotFound("Product not found.");

        return Ok(product);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return BadRequest("Keyword is required.");

        var products = await _context.Products
            .Where(x =>
                x.IsActive &&
                x.QuantityAvailable > 0 &&
                (
                    x.Name.Contains(keyword) ||
                    x.Brand.Contains(keyword)
                ))
            .OrderByDescending(x => x.CreatedAt)
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

    [HttpGet("supplier/{supplierId:guid}")]
    public async Task<IActionResult> GetSupplierProducts(Guid supplierId)
    {
        var products = await _context.Products
            .Where(x => x.SupplierId == supplierId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(products);
    }

    [HttpPost("upload")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> UploadProduct([FromForm] CreateProductUploadDto dto)
    {
        if (dto.SupplierId == Guid.Empty)
            return BadRequest("SupplierId is required.");

        var supplierExists = await _context.Suppliers
            .AnyAsync(s => s.Id == dto.SupplierId);

        if (!supplierExists)
            return BadRequest("Supplier not found.");

        string? imageUrl = null;

        if (dto.Image != null && dto.Image.Length > 0)
        {
            var uploadsFolder = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                "uploads",
                "products"
            );

            Directory.CreateDirectory(uploadsFolder);

            var extension = Path.GetExtension(dto.Image.FileName);
            var fileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            await using var stream = new FileStream(filePath, FileMode.Create);
            await dto.Image.CopyToAsync(stream);

            imageUrl = $"/uploads/products/{fileName}";
        }

        var product = new Product
        {
            Id = Guid.NewGuid(),
            SupplierId = dto.SupplierId,
            PartNumber = dto.PartNumber,
            Brand = dto.Brand,
            Name = dto.Name,
            Description = dto.Description,
            ImageUrl = imageUrl,
            Price = dto.Price,
            QuantityAvailable = dto.QuantityAvailable,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        return Ok(product);
    }
}