using Alpha.API.Data;
using Alpha.API.Extensions;
using Alpha.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Alpha.API.Controllers;
[ApiController, Authorize(Roles="admin"), Route("api/payout-reconciliation")]
public sealed class PayoutReconciliationController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> Queue(string kind="settlement", Guid? after=null, int limit=20, CancellationToken ct=default)
        => Execute(async () => await new PayoutReconciliationService(db).QueueAsync(kind,after,limit,User.GetUserId(),ct));
    [HttpGet("{kind}/{id:guid}")]
    public Task<IActionResult> Inspect(string kind, Guid id, CancellationToken ct)
        => Execute(async () => await new PayoutReconciliationService(db).InspectAsync(kind,id,User.GetUserId(),ct));
    [HttpPost("{kind}/{id:guid}/captures")]
    public Task<IActionResult> Capture(string kind, Guid id, ReconciliationCaptureRequest request, CancellationToken ct)
        => Execute(async () => await new PayoutReconciliationService(db).CaptureAsync(kind,id,request,User.GetUserId(),ct));
    [HttpGet("{kind}/{id:guid}/history")]
    public Task<IActionResult> History(string kind, Guid id, Guid? after=null, int limit=20, CancellationToken ct=default)
        => Execute(async () => await new PayoutReconciliationService(db).HistoryAsync(kind,id,after,limit,User.GetUserId(),ct));
    private async Task<IActionResult> Execute(Func<Task<object>> action)
    {
        try {return Ok(await action());}
        catch(UnauthorizedAccessException) {return Forbid();}
        catch(KeyNotFoundException ex) {return NotFound(new {message=ex.Message});}
        catch(ArgumentException ex) {return BadRequest(new {message=ex.Message});}
        catch(InvalidOperationException ex) {return Conflict(new {code="reconciliation_review_required",message=ex.Message});}
    }
}
