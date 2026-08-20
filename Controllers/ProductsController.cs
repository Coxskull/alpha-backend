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
            .AsNoTracking()
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
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.IsActive);

        if (product == null)
            return NotFound(new
            {
                message = "Product not found."
            });

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
        {
            return BadRequest(new
            {
                message = "Keyword is required."
            });
        }

        keyword = keyword.Trim();

        var products = await _context.Products
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                x.QuantityAvailable > 0 &&
                (
                    x.Name.Contains(keyword) ||
                    x.Brand.Contains(keyword) ||
                    x.PartNumber.Contains(keyword)
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
        [FromBody] CreateProductDto dto)
    {
        if (dto.SupplierId == Guid.Empty)
        {
            return BadRequest(new
            {
                message = "SupplierId is required."
            });
        }

        var supplier = await FindSupplier(dto.SupplierId);

        if (supplier == null)
        {
            return NotFound(new
            {
                message = "Supplier not found."
            });
        }

        if (dto.Price <= 0)
        {
            return BadRequest(new
            {
                message = "Price must be greater than zero."
            });
        }

        if (dto.QuantityAvailable < 0)
        {
            return BadRequest(new
            {
                message = "Quantity cannot be negative."
            });
        }

        var product = new Product
        {
            Id = Guid.NewGuid(),

            SupplierId = supplier.Id,

            PartNumber = dto.PartNumber?.Trim() ?? string.Empty,
            Brand = dto.Brand?.Trim() ?? string.Empty,
            Name = dto.Name?.Trim() ?? string.Empty,
            Description = dto.Description?.Trim() ?? string.Empty,

            ImageUrl = string.IsNullOrWhiteSpace(dto.ImageUrl)
                ? "/uploads/products/default-product.png"
                : dto.ImageUrl,

            Price = dto.Price,
            QuantityAvailable = dto.QuantityAvailable,

            IsActive = true,

            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,

            Currency = NormalizeCurrency(dto.Currency),
            CountryCode = NormalizeCountry(dto.CountryCode)
        };

        _context.Products.Add(product);

        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetProduct),
            new { id = product.Id },
            product);
    }

    // ============================================================
    // GET PRODUCTS BY SUPPLIER ID
    // ============================================================

    [HttpGet("supplier/{supplierId:guid}")]
    public async Task<IActionResult> GetSupplierProducts(
        Guid supplierId)
    {
        var supplier = await _context.Suppliers
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == supplierId);

        if (supplier == null)
        {
            return NotFound(new
            {
                message = "Supplier not found.",
                supplierId
            });
        }

        var products = await _context.Products
            .AsNoTracking()
            .Where(x =>
                x.SupplierId == supplier.Id &&
                x.IsActive)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        // IMPORTANT:
        // Return an ARRAY because the frontend inventory page
        // expects response.data to be Product[].
        return Ok(products);
    }

    // ============================================================
    // GET SUPPLIER PRODUCTS BY USER ID
    // ============================================================

    [HttpGet("supplier/user/{userId:guid}")]
    public async Task<IActionResult> GetSupplierProductsByUser(
        Guid userId)
    {
        var supplier = await _context.Suppliers
            .AsNoTracking()
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
            .AsNoTracking()
            .Where(x =>
                x.SupplierId == supplier.Id &&
                x.IsActive)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        // IMPORTANT:
        // Return only products so the frontend can directly
        // consume response.data as Product[].
        return Ok(products);
    }

    // ============================================================
    // GET SUPPLIER ID BY USER ID
    // ============================================================

    [HttpGet("supplier-id/user/{userId:guid}")]
    public async Task<IActionResult> GetSupplierIdByUser(
        Guid userId)
    {
        var supplier = await _context.Suppliers
            .AsNoTracking()
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

        return Ok(new
        {
            supplierId = supplier.Id,
            userId = supplier.UserId
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
        {
            return BadRequest(new
            {
                message = "SupplierId is required."
            });
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return BadRequest(new
            {
                message = "Product name is required."
            });
        }

        if (string.IsNullOrWhiteSpace(dto.Brand))
        {
            return BadRequest(new
            {
                message = "Brand is required."
            });
        }

        if (dto.Price <= 0)
        {
            return BadRequest(new
            {
                message = "Price must be greater than zero."
            });
        }

        if (dto.QuantityAvailable < 0)
        {
            return BadRequest(new
            {
                message = "Quantity cannot be negative."
            });
        }

        var supplier = await FindSupplier(dto.SupplierId);

        if (supplier == null)
        {
            return BadRequest(new
            {
                message = "Supplier not found."
            });
        }

        string imageUrl =
            "/uploads/products/default-product.png";

        string? imageBase64 = null;

        if (dto.Image != null &&
            dto.Image.Length > 0)
        {
            if (!dto.Image.ContentType
                .StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    message = "Only image files are allowed."
                });
            }

            await using var ms = new MemoryStream();

            await dto.Image.CopyToAsync(ms);

            var bytes = ms.ToArray();

            var base64 = Convert.ToBase64String(bytes);

            imageBase64 =
                $"data:{dto.Image.ContentType};base64,{base64}";

            imageUrl = imageBase64;
        }

        var product = new Product
        {
            Id = Guid.NewGuid(),

            // Always store the actual Supplier.Id.
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

            ImageUrl = imageUrl,

            // Your database has image_base64 as well.
            ImageBase64 = imageBase64,

            Price = dto.Price,

            QuantityAvailable =
                dto.QuantityAvailable,

            IsActive = true,

            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,

            Currency = NormalizeCurrency(dto.Currency),
            CountryCode = NormalizeCountry(dto.CountryCode)
        };

        _context.Products.Add(product);

        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetProduct),
            new { id = product.Id },
            product);
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
        {
            return BadRequest(new
            {
                message = "SupplierId is required."
            });
        }

        var supplier = await FindSupplier(dto.SupplierId);

        if (supplier == null)
        {
            return BadRequest(new
            {
                message = "Supplier not found."
            });
        }

        var product = await _context.Products
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.SupplierId == supplier.Id);

        if (product == null)
        {
            return NotFound(new
            {
                message =
                    "Product not found or does not belong to this supplier."
            });
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return BadRequest(new
            {
                message = "Product name is required."
            });
        }

        if (string.IsNullOrWhiteSpace(dto.Brand))
        {
            return BadRequest(new
            {
                message = "Brand is required."
            });
        }

        if (dto.Price <= 0)
        {
            return BadRequest(new
            {
                message = "Price must be greater than zero."
            });
        }

        if (dto.QuantityAvailable < 0)
        {
            return BadRequest(new
            {
                message = "Quantity cannot be negative."
            });
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

        product.Price = dto.Price;

        product.QuantityAvailable =
            dto.QuantityAvailable;

        product.IsActive = dto.IsActive;

        product.UpdatedAt = DateTime.UtcNow;

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
                .StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    message = "Only image files are allowed."
                });
            }

            await using var ms =
                new MemoryStream();

            await dto.Image.CopyToAsync(ms);

            var bytes = ms.ToArray();

            var base64 =
                Convert.ToBase64String(bytes);

            var dataUrl =
                $"data:{dto.Image.ContentType};base64,{base64}";

            product.ImageUrl = dataUrl;
            product.ImageBase64 = dataUrl;
        }

        await _context.SaveChangesAsync();

        return Ok(product);
    }

    // ============================================================
    // DELETE PRODUCT
    //
    // Soft delete:
    // IsActive = false
    // ============================================================

    [HttpDelete("{id:guid}/supplier/{supplierId:guid}")]
    public async Task<IActionResult> DeleteProduct(
        Guid id,
        Guid supplierId)
    {
        var supplier = await FindSupplier(supplierId);

        if (supplier == null)
        {
            return NotFound(new
            {
                message = "Supplier not found."
            });
        }

        var product = await _context.Products
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.SupplierId == supplier.Id);

        if (product == null)
        {
            return NotFound(new
            {
                message =
                    "Product not found or does not belong to this supplier."
            });
        }

        product.IsActive = false;
        product.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Product deleted successfully.",
            productId = product.Id
        });
    }

    // ============================================================
    // PRIVATE SUPPLIER RESOLUTION
    //
    // Accepts either:
    // 1. Suppliers.Id
    // 2. Users.Id
    // ============================================================

    private async Task<Supplier?> FindSupplier(
        Guid id)
    {
        var supplier = await _context.Suppliers
            .FirstOrDefaultAsync(s =>
                s.Id == id);

        if (supplier != null)
            return supplier;

        return await _context.Suppliers
            .FirstOrDefaultAsync(s =>
                s.UserId == id);
    }

    // ============================================================
    // NORMALIZATION
    // ============================================================

    private static string NormalizeCurrency(
        string? currency)
    {
        return string.IsNullOrWhiteSpace(currency)
            ? "PHP"
            : currency.Trim().ToUpperInvariant();
    }

    private static string NormalizeCountry(
        string? countryCode)
    {
        return string.IsNullOrWhiteSpace(countryCode)
            ? "PH"
            : countryCode.Trim().ToUpperInvariant();
    }
}