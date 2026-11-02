using ConstantLearning.Features.Identity.Auth;
using ConstantLearning.Features.Learning.Sessions.Start;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConstantLearning.Features.Learning.Sessions.Results;

[ApiController]
[Authorize]
[Route("api/v1/sessions/{sessionId:guid}/items/{itemId:guid}/continue")]
public sealed class ContinueSessionController(ContinueSessionService sessions) : ControllerBase
{
    [HttpPost(Name = "continueSession")]
    [ProducesResponseType<SessionResponse>(200)]
    [ProducesResponseType<SessionError>(401)]
    [ProducesResponseType<SessionError>(404)]
    [ProducesResponseType<SessionError>(409)]
    public async Task<IActionResult> Post(Guid sessionId, Guid itemId, CancellationToken ct)
    {
        if (User.GetLearnerId() is not { } learnerId) return NotFound(new SessionError("session.item_not_found", "Картку не знайдено."));
        var result = await sessions.ContinueAsync(learnerId, sessionId, itemId, ct);
        return StatusCode(result.Status, (object?)result.Response ?? result.Error);
    }
}
