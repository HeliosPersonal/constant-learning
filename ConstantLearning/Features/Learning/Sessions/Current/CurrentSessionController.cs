using ConstantLearning.Features.Identity.Auth;
using ConstantLearning.Features.Learning.Sessions.Start;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConstantLearning.Features.Learning.Sessions.Current;

[ApiController]
[Authorize]
[Route("api/v1/sessions/current")]
public sealed class CurrentSessionController(CurrentSessionService sessions) : ControllerBase
{
    [HttpGet(Name = "getCurrentSession")]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<SessionError>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<SessionError>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var session = User.GetLearnerId() is { } learnerId
            ? await sessions.GetAsync(learnerId, cancellationToken) : null;
        return session is null
            ? NotFound(new SessionError("session.not_found", "Активного заняття немає — можна почати нове."))
            : Ok(session);
    }
}
