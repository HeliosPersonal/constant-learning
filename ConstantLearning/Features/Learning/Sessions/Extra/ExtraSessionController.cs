using ConstantLearning.Features.Identity.Auth;
using ConstantLearning.Features.Learning.Sessions.Start;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConstantLearning.Features.Learning.Sessions.Extra;

[ApiController]
[Authorize]
[Route("api/v1/sessions")]
public sealed class ExtraSessionController(
    StartExtraWordService extraWords, StartExtraPracticeService extraPractice) : ControllerBase
{
    [HttpPost("extra-word", Name = "startExtraWord")]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<SessionError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<SessionError>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<SessionError>(StatusCodes.Status429TooManyRequests)]
    public Task<IActionResult> ExtraWord(CancellationToken cancellationToken) =>
        RunAsync(extraWords.StartAsync, cancellationToken);

    [HttpPost("extra-practice", Name = "startExtraPractice")]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<SessionError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<SessionError>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<SessionError>(StatusCodes.Status429TooManyRequests)]
    public Task<IActionResult> ExtraPractice(CancellationToken cancellationToken) =>
        RunAsync(extraPractice.StartAsync, cancellationToken);

    private async Task<IActionResult> RunAsync(
        Func<Guid, CancellationToken, Task<ExtraStartResult?>> start, CancellationToken cancellationToken)
    {
        var result = User.GetLearnerId() is { } learnerId ? await start(learnerId, cancellationToken) : null;
        if (result is null)
        {
            return NotFound(new SessionError("learner.not_onboarded", "Спершу налаштуйте тренер."));
        }

        return result.Error is { } error
            ? StatusCode(result.Status, error)
            : StatusCode(result.Status, result.Session);
    }
}
