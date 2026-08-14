using Alpha.API.DTOs;
using Alpha.API.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Alpha.API.Controllers;

[ApiController]
[Route("api/test/auto-parts-commission")]
public class AutoPartsCommissionTestController : ControllerBase
{
    private readonly AutoPartsCommissionService _commissionService;

    public AutoPartsCommissionTestController(
        AutoPartsCommissionService commissionService)
    {
        _commissionService = commissionService;
    }

    [HttpPost("calculate")]
    public async Task<ActionResult<AutoPartsCommissionResultDtos>> Calculate(
        [FromBody] AutoPartsCommissionTestDtos request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _commissionService.CalculateAsync(
                request.Subtotal,
                request.Currency,
                DateTime.UtcNow,
                cancellationToken);

            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }
}