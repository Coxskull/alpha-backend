using Alpha.API.Data;
using Alpha.API.DTOs;
using Alpha.API.Extensions;
using Alpha.API.Models;
using Alpha.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Alpha.API.Controllers;

[ApiController, Authorize(Roles = "admin"), Route("api/financial-conflicts")]
public sealed class FinancialConflictsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Queue([FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100 || page > 10000) return BadRequest("Invalid pagination.");
        var rows = await db.Payments.AsNoTracking().Where(x => x.RefundEvidenceConflict).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).Select(x => new { paymentId = x.Id, x.OrderId, x.Currency,
                x.Amount, x.RefundedAmount, holdReleased = false,
                reviewCount = db.Set<FinancialConflictReview>().Count(r => r.PaymentId == x.Id) }).ToListAsync(ct);
        return Ok(new { page, pageSize, rows });
    }

    [HttpPost("payments/{paymentId:guid}/reviews")]
    public Task<IActionResult> Submit(Guid paymentId, FinancialConflictReviewDto request, CancellationToken ct)
        => Execute(async () => await new FinancialConflictReviewService(db).SubmitAsync(paymentId, request, User.GetUserId(), ct));

    [HttpPost("reviews/{reviewId:guid}/approval")]
    public Task<IActionResult> Approve(Guid reviewId, FinancialConflictApprovalDto request, CancellationToken ct)
        => Execute(async () => await new FinancialConflictReviewService(db).ApproveAsync(reviewId, request, User.GetUserId(), ct));

    [HttpPost("reviews/{reviewId:guid}/recovery-releases")]
    public Task<IActionResult> Release(Guid reviewId, FinancialRecoveryReleaseDto request, CancellationToken ct)
        => Execute(async () => await new FinancialRecoveryReleaseService(db).ReleaseAsync(reviewId, request, User.GetUserId(), ct));

    [HttpGet("reviews/{reviewId:guid}")]
    public Task<IActionResult> Status(Guid reviewId, CancellationToken ct)
        => Execute(async () => await new FinancialConflictReviewService(db).StatusAsync(reviewId, ct));

    private async Task<IActionResult> Execute(Func<Task<object>> action)
    {
        try { return Ok(await action()); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }
}
