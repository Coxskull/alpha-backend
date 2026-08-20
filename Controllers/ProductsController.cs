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
            PartNumber = dto.PartNumber ?? string.Empty,
            Brand = dto.Brand ?? string.Empty,
            Name = dto.Name ?? string.Empty,
            Description = dto.Description ?? string.Empty,
            ImageUrl = dto.ImageUrl ??
         "/uploads/products/default-product.png",
            Price = dto.Price,
            QuantityAvailable = dto.QuantityAvailable,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Currency = string.IsNullOrWhiteSpace(dto.Currency)
         ? "MXN"
         : dto.Currency.Trim().ToUpperInvariant(),
            CountryCode = string.IsNullOrWhiteSpace(dto.CountryCode)
         ? "MX"
         : dto.CountryCode.Trim().ToUpperInvariant()
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        return Ok(product);
    }

    [HttpGet("supplier/{supplierId:guid}")]
    public async Task<IActionResult> GetSupplierProducts(Guid supplierId)
    {
        var products = await _context.Products
            .Where(x => x.SupplierId == supplierId && x.IsActive)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(products);
    }
    [HttpPost("upload")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> UploadProduct(
        [FromForm] CreateProductUploadDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (dto.SupplierId == Guid.Empty)
            return BadRequest("SupplierId is required.");

        // First try Supplier.Id
        var supplier = await _context.Suppliers
            .FirstOrDefaultAsync(s => s.Id == dto.SupplierId);

        // If not found, try Supplier.user_id
        if (supplier == null)
        {
            supplier = await _context.Suppliers
                .FirstOrDefaultAsync(s => s.UserId == dto.SupplierId);
        }

        if (supplier == null)
            return BadRequest("Supplier not found.");

        string imageUrl = "";

        if (dto.Image != null && dto.Image.Length > 0)
        {
            // Optional but recommended validation
            if (!dto.Image.ContentType.StartsWith("image/"))
                return BadRequest("Only image files are allowed.");

            await using var ms = new MemoryStream();
            await dto.Image.CopyToAsync(ms);

            var base64 = Convert.ToBase64String(ms.ToArray());

            imageUrl = $"data:{dto.Image.ContentType};base64,{base64}";
        }

        var product = new Product
        {
            Id = Guid.NewGuid(),

            // IMPORTANT:
            // Always save the actual suppliers.Id
            SupplierId = supplier.Id,

            PartNumber = dto.PartNumber?.Trim() ?? string.Empty,
            Brand = dto.Brand?.Trim() ?? string.Empty,
            Name = dto.Name?.Trim() ?? string.Empty,
            Description = dto.Description?.Trim() ?? string.Empty,

            ImageUrl = string.IsNullOrWhiteSpace(imageUrl)
                ? "/uploads/products/default-product.png"
                : imageUrl,

            Price = dto.Price,
            QuantityAvailable = dto.QuantityAvailable,

            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,

            Currency = string.IsNullOrWhiteSpace(dto.Currency)
                ? "MXN"
                : dto.Currency.Trim().ToUpperInvariant(),

            CountryCode = string.IsNullOrWhiteSpace(dto.CountryCode)
                ? "MX"
                : dto.CountryCode.Trim().ToUpperInvariant()
        };

        _context.Products.Add(product);

        await _context.SaveChangesAsync();

        return Ok(product);
    }

    [HttpPut("{id:guid}")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> UpdateProduct(Guid id, [FromForm] UpdateProductUploadDto dto)
    {
        var supplier = await _context.Suppliers
     .FirstOrDefaultAsync(s => s.Id == dto.SupplierId);

        if (supplier == null)
        {
            supplier = await _context.Suppliers
                .FirstOrDefaultAsync(s => s.UserId == dto.SupplierId);
        }

        if (supplier == null)
            return BadRequest("Supplier not found.");

        var product = await _context.Products
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.SupplierId == supplier.Id);

        if (product == null)
            return NotFound("Product not found or does not belong to this supplier.");

        product.PartNumber = dto.PartNumber ?? "";
        product.Brand = dto.Brand;
        product.Name = dto.Name;
        product.Description = dto.Description ?? "";
        product.Price = dto.Price;
        product.QuantityAvailable = dto.QuantityAvailable;
        product.IsActive = dto.IsActive;
        product.UpdatedAt = DateTime.UtcNow;

        product.Currency =
            string.IsNullOrWhiteSpace(dto.Currency)
                ? product.Currency
                : dto.Currency.Trim().ToUpperInvariant();

        product.CountryCode =
            string.IsNullOrWhiteSpace(dto.CountryCode)
                ? product.CountryCode
                : dto.CountryCode.Trim().ToUpperInvariant();

        if (dto.Image != null && dto.Image.Length > 0)
        {
            await using var ms = new MemoryStream();
            await dto.Image.CopyToAsync(ms);
            var base64 = Convert.ToBase64String(ms.ToArray());
            product.ImageUrl = $"data:{dto.Image.ContentType};base64,{base64}";
        }

        await _context.SaveChangesAsync();

        return Ok(product);
    }

    [HttpDelete("{id:guid}/supplier/{supplierId:guid}")]
    public async Task<IActionResult> DeleteProduct(Guid id, Guid supplierId)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(x => x.Id == id && x.SupplierId == supplierId);

        if (product == null)
            return NotFound("Product not found or does not belong to this supplier.");

        product.IsActive = false;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Product deleted successfully."
        });
    }

    [HttpGet("supplier/user/{userId:guid}")]
    public async Task<IActionResult> GetSupplierProductsByUser(Guid userId)
    {
        var supplier = await _context.Suppliers
            .FirstOrDefaultAsync(s => s.user_id == userId);

        if (supplier == null)
            return NotFound("Supplier profile not found.");

        var products = await _context.Products
            .Where(x => x.SupplierId == supplier.Id)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(products);
    }
}