using ConstantLearning.Features.Identity.Auth;
using ConstantLearning.Features.Identity.Learners;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConstantLearning.Features.Identity.ProgressReset;

[ApiController]
[Authorize]
[Route("api/v1/learners/me/progress")]
public sealed class ProgressResetController(ProgressResetService service) : ControllerBase
{
    [HttpPost("reset", Name = "resetLearnerProgress")]
    [ProducesResponseType<ProgressResetResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<IdentityError>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<IdentityError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<IdentityError>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ResetLearnerProgress(CancellationToken cancellationToken)
    {
        if (User.GetLearnerId() is not { } learnerId)
        {
            return NotOnboarded();
        }

        var outcome = await service.ResetAsync(learnerId, cancellationToken);
        return outcome switch
        {
            ProgressResetOutcome.Erased => Ok(new ProgressResetResponse(true)),
            ProgressResetOutcome.AlreadyEmpty => Ok(new ProgressResetResponse(false)),
            ProgressResetOutcome.NotOnboarded => NotOnboarded(),
            _ => Failed()
        };
    }

    private NotFoundObjectResult NotOnboarded() => NotFound(new IdentityError("learner.not_onboarded", "Спершу налаштуйте тренер."));

    private ObjectResult Failed() => StatusCode(StatusCodes.Status500InternalServerError,
        new IdentityError("progress_reset.failed", "Не вдалося скинути прогрес. Нічого не змінено — спробуйте ще раз."));
}
