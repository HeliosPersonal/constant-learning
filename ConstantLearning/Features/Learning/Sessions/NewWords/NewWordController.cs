using ConstantLearning.Features.Identity.Auth;
using ConstantLearning.Features.Learning.Sessions.Start;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConstantLearning.Features.Learning.Sessions.NewWords;

[ApiController]
[Authorize]
[Route("api/v1/sessions/{sessionId:guid}/items/{itemId:guid}")]
public sealed class NewWordController(NewWordService words) : ControllerBase
{
    [HttpPost("acknowledge", Name = "acknowledgeNewWordCard")]
    [ProducesResponseType<SessionProgressResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<SessionError>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<SessionError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<SessionError>(StatusCodes.Status409Conflict)]
    public Task<IActionResult> Acknowledge(Guid sessionId, Guid itemId, CancellationToken cancellationToken) =>
        ActAsync(sessionId, itemId, false, cancellationToken);

    [HttpPost("already-known", Name = "markAlreadyKnown")]
    [ProducesResponseType<AlreadyKnownResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<SessionError>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<SessionError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<SessionError>(StatusCodes.Status409Conflict)]
    public Task<IActionResult> AlreadyKnown(Guid sessionId, Guid itemId, CancellationToken cancellationToken) =>
        ActAsync(sessionId, itemId, true, cancellationToken);

    private async Task<IActionResult> ActAsync(Guid sessionId, Guid itemId, bool alreadyKnown, CancellationToken cancellationToken)
    {
        if (User.GetLearnerId() is not { } learnerId)
            return NotFound(new SessionError("session.item_not_found", "Картку не знайдено."));
        var result = await words.ActAsync(learnerId, sessionId, itemId, alreadyKnown, cancellationToken);
        return StatusCode(result.StatusCode, result.Response ?? result.Error);
    }
}
