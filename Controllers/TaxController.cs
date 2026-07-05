using Alpha.API.Data;
using Alpha.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace Alpha.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "admin,dispatcher")]
public class TaxController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly TaxEngineService _taxEngine;

    public TaxController(AppDbContext context, TaxEngineService taxEngine)
    {
        _context = context;
        _taxEngine = taxEngine;
    }

    [HttpGet("rules")]
    public async Task<IActionResult> GetRules()
    {
        return Ok(await _context.TaxRules
            .OrderByDescending(x => x.EffectiveFrom)
            .ToListAsync());
    }

    [HttpPost("orders/{orderId}/calculate")]
    public async Task<IActionResult> CalculateOrderTax(Guid orderId)
    {
        var result = await _taxEngine.CalculateOrderTaxes(orderId, "MX", null, "MXN");
        return Ok(result);
    }

    [HttpGet("orders/{orderId}")]
    public async Task<IActionResult> GetOrderTax(Guid orderId)
    {
        var calculations = await _context.TaxCalculations
            .Where(x => x.OrderId == orderId)
            .ToListAsync();

        var ledger = await _context.TaxLedgerEntries
            .Where(x => x.OrderId == orderId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(new { calculations, ledger });
    }
}