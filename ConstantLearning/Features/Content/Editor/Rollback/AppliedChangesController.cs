using ConstantLearning.Features.Identity.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConstantLearning.Features.Content.Editor.Rollback;

[ApiController]
[Authorize(Policy = TelegramAuthentication.ContentEditorPolicy)]
[Route("api/v1/editor/applied-changes")]
public sealed class AppliedChangesController(AppliedChangesService changes) : ControllerBase
{
    [HttpGet(Name = "listAppliedChanges")]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (AppliedChangesRequest.Read(Request.Query, out var request) is { } error) return BadRequest(error);
        var page = await changes.ListAsync(request!, cancellationToken);
        return page is null ? BadRequest(AppliedChangesRequest.Invalid()) : Ok(page);
    }

    [HttpPost("{change_id}/rollback", Name = "rollbackAppliedChange")]
    public async Task<IActionResult> Rollback(string change_id, CancellationToken cancellationToken)
    {
        if (!Guid.TryParseExact(change_id, "D", out var changeId)) return NotFound(AppliedChangesService.NotFound());
        var response = await changes.RollbackAsync(changeId, User.GetLearnerId(), cancellationToken);
        return StatusCode(response.Status, response.Result is { } result ? result : response.Error);
    }
}
