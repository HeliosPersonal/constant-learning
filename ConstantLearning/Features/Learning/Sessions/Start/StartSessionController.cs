using ConstantLearning.Features.Identity.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConstantLearning.Features.Learning.Sessions.Start;

[ApiController]
[Authorize]
[Route("api/v1/sessions")]
public sealed class StartSessionController(StartSessionService sessions) : ControllerBase
{
    [HttpPost(Name = "startSession")]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<SessionError>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<SessionError>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Start(CancellationToken cancellationToken)
    {
        var result = User.GetLearnerId() is { } learnerId
            ? await sessions.StartAsync(learnerId, cancellationToken) : null;
        if (result is null)
        {
            return NotFound(new SessionError("learner.not_onboarded", "Спершу налаштуйте тренер."));
        }

        return StatusCode(result.Created ? StatusCodes.Status201Created : StatusCodes.Status200OK, result.Session);
    }
}
