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

    // ============================================================
    // GET ALL ACTIVE PRODUCTS
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> GetProducts()
    {
        var products = await _context.Products
            .Where(x =>
                x.IsActive &&
                x.QuantityAvailable > 0)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(products);
    }

    // ============================================================
    // GET PRODUCT BY ID
    // ============================================================

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

    // ============================================================
    // SEARCH
    // ============================================================

    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string keyword)
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

    // ============================================================
    // CREATE PRODUCT - JSON
    // ============================================================

    [HttpPost]
    public async Task<IActionResult> CreateProduct(
        CreateProductDto dto)
    {
        var supplier = await _context.Suppliers
            .FirstOrDefaultAsync(s =>
                s.Id == dto.SupplierId);

        if (supplier == null)
            return NotFound("Supplier not found.");

        var product = new Product
        {
            Id = Guid.NewGuid(),

            SupplierId = supplier.Id,

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

    // ============================================================
    // GET PRODUCTS BY ACTUAL SUPPLIER ID
    // ============================================================

    [HttpGet("supplier/{supplierId:guid}")]
    public async Task<IActionResult> GetSupplierProducts(
        Guid supplierId)
    {
        var supplierExists = await _context.Suppliers
            .AnyAsync(s => s.Id == supplierId);

        if (!supplierExists)
            return NotFound("Supplier not found.");

        var products = await _context.Products
            .Where(x =>
                x.SupplierId == supplierId &&
                x.IsActive)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(products);
    }

    // ============================================================
    // GET SUPPLIER + PRODUCTS BY USER ID
    //
    // This is the important endpoint for the frontend.
    //
    // alpha_user.id
    //       ↓
    // Suppliers.UserId
    //       ↓
    // Suppliers.Id
    //       ↓
    // Products.SupplierId
    // ============================================================

    [HttpGet("supplier/user/{userId:guid}")]
    public async Task<IActionResult> GetSupplierProductsByUser(
        Guid userId)
    {
        var supplier = await _context.Suppliers
            .FirstOrDefaultAsync(s =>
                s.UserId == userId);

        if (supplier == null)
        {
            return NotFound(new
            {
                message = "Supplier profile not found.",
                userId
            });
        }

        var products = await _context.Products
            .Where(x =>
                x.SupplierId == supplier.Id &&
                x.IsActive)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(new
        {
            supplierId = supplier.Id,
            userId = supplier.UserId,
            products
        });
    }

    // ============================================================
    // CREATE PRODUCT WITH IMAGE
    // ============================================================

    [HttpPost("upload")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> UploadProduct(
        [FromForm] CreateProductUploadDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (dto.SupplierId == Guid.Empty)
            return BadRequest("SupplierId is required.");

        // First: assume supplied ID is Suppliers.Id
        var supplier = await _context.Suppliers
            .FirstOrDefaultAsync(s =>
                s.Id == dto.SupplierId);

        // Second: allow supplied ID to be Users.Id
        if (supplier == null)
        {
            supplier = await _context.Suppliers
                .FirstOrDefaultAsync(s =>
                    s.UserId == dto.SupplierId);
        }

        if (supplier == null)
            return BadRequest("Supplier not found.");

        string imageUrl = "";

        if (dto.Image != null &&
            dto.Image.Length > 0)
        {
            if (!dto.Image.ContentType
                .StartsWith("image/"))
            {
                return BadRequest(
                    "Only image files are allowed.");
            }

            await using var ms =
                new MemoryStream();

            await dto.Image.CopyToAsync(ms);

            var base64 =
                Convert.ToBase64String(ms.ToArray());

            imageUrl =
                $"data:{dto.Image.ContentType};base64,{base64}";
        }

        var product = new Product
        {
            Id = Guid.NewGuid(),

            // ALWAYS store actual Suppliers.Id
            SupplierId = supplier.Id,

            PartNumber =
                dto.PartNumber?.Trim() ??
                string.Empty,

            Brand =
                dto.Brand?.Trim() ??
                string.Empty,

            Name =
                dto.Name?.Trim() ??
                string.Empty,

            Description =
                dto.Description?.Trim() ??
                string.Empty,

            ImageUrl =
                string.IsNullOrWhiteSpace(imageUrl)
                    ? "/uploads/products/default-product.png"
                    : imageUrl,

            Price = dto.Price,

            QuantityAvailable =
                dto.QuantityAvailable,

            IsActive = true,

            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,

            Currency =
                string.IsNullOrWhiteSpace(dto.Currency)
                    ? "MXN"
                    : dto.Currency
                        .Trim()
                        .ToUpperInvariant(),

            CountryCode =
                string.IsNullOrWhiteSpace(dto.CountryCode)
                    ? "MX"
                    : dto.CountryCode
                        .Trim()
                        .ToUpperInvariant()
        };

        _context.Products.Add(product);

        await _context.SaveChangesAsync();

        return Ok(product);
    }

    // ============================================================
    // UPDATE PRODUCT
    // ============================================================

    [HttpPut("{id:guid}")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> UpdateProduct(
        Guid id,
        [FromForm] UpdateProductUploadDto dto)
    {
        if (dto.SupplierId == Guid.Empty)
            return BadRequest("SupplierId is required.");

        // Try actual Suppliers.Id
        var supplier = await _context.Suppliers
            .FirstOrDefaultAsync(s =>
                s.Id == dto.SupplierId);

        // Otherwise try Users.Id
        if (supplier == null)
        {
            supplier = await _context.Suppliers
                .FirstOrDefaultAsync(s =>
                    s.UserId == dto.SupplierId);
        }

        if (supplier == null)
            return BadRequest("Supplier not found.");

        var product = await _context.Products
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.SupplierId == supplier.Id);

        if (product == null)
        {
            return NotFound(
                "Product not found or does not belong to this supplier.");
        }

        product.PartNumber =
            dto.PartNumber?.Trim() ??
            string.Empty;

        product.Brand =
            dto.Brand?.Trim() ??
            string.Empty;

        product.Name =
            dto.Name?.Trim() ??
            string.Empty;

        product.Description =
            dto.Description?.Trim() ??
            string.Empty;

        product.Price =
            dto.Price;

        product.QuantityAvailable =
            dto.QuantityAvailable;

        product.IsActive =
            dto.IsActive;

        product.UpdatedAt =
            DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(dto.Currency))
        {
            product.Currency =
                dto.Currency
                    .Trim()
                    .ToUpperInvariant();
        }

        if (!string.IsNullOrWhiteSpace(dto.CountryCode))
        {
            product.CountryCode =
                dto.CountryCode
                    .Trim()
                    .ToUpperInvariant();
        }

        if (dto.Image != null &&
            dto.Image.Length > 0)
        {
            if (!dto.Image.ContentType
                .StartsWith("image/"))
            {
                return BadRequest(
                    "Only image files are allowed.");
            }

            await using var ms =
                new MemoryStream();

            await dto.Image.CopyToAsync(ms);

            var base64 =
                Convert.ToBase64String(ms.ToArray());

            product.ImageUrl =
                $"data:{dto.Image.ContentType};base64,{base64}";
        }

        await _context.SaveChangesAsync();

        return Ok(product);
    }

    // ============================================================
    // DELETE PRODUCT
    //
    // Accepts either:
    // - actual Suppliers.Id
    // - logged-in Users.Id
    // ============================================================

    [HttpDelete("{id:guid}/supplier/{supplierId:guid}")]
    public async Task<IActionResult> DeleteProduct(
        Guid id,
        Guid supplierId)
    {
        // First try actual Suppliers.Id
        var supplier = await _context.Suppliers
            .FirstOrDefaultAsync(s =>
                s.Id == supplierId);

        // Otherwise try Users.Id
        if (supplier == null)
        {
            supplier = await _context.Suppliers
                .FirstOrDefaultAsync(s =>
                    s.UserId == supplierId);
        }

        if (supplier == null)
            return NotFound("Supplier not found.");

        var product = await _context.Products
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.SupplierId == supplier.Id);

        if (product == null)
        {
            return NotFound(
                "Product not found or does not belong to this supplier.");
        }

        product.IsActive = false;
        product.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Product deleted successfully."
        });
    }

    [HttpGet("supplier-id/user/{userId:guid}")]
    public async Task<IActionResult> GetSupplierIdByUser(Guid userId)
    {
        var supplier = await _context.Suppliers
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId);

        if (supplier == null)
            return NotFound(new
            {
                message = "Supplier profile not found.",
                userId
            });

        return Ok(new
        {
            supplierId = supplier.Id
        });
    }
}