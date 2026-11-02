using ConstantLearning.Features.Identity.Auth;
using ConstantLearning.Features.Learning.Sessions.Start;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConstantLearning.Features.Learning.Today;

[ApiController]
[Authorize]
[Route("api/v1/today")]
public sealed class TodayController(TodayService today) : ControllerBase
{
    [HttpGet(Name = "getToday")]
    [ProducesResponseType<TodayResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<SessionError>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<SessionError>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var response = User.GetLearnerId() is { } learnerId
            ? await today.GetAsync(learnerId, cancellationToken) : null;
        return response is null
            ? NotFound(new SessionError("learner.not_onboarded", "Спочатку завершіть налаштування навчання."))
            : Ok(response);
    }
}
