using System.Text.Json;
using ConstantLearning.Features.Identity.Auth;
using ConstantLearning.Features.Identity.Learners;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConstantLearning.Features.Identity.Settings;

[ApiController]
[Authorize]
[Route("api/v1/learners/me/settings")]
public sealed class LearnerSettingsController(LearnerSettingsService settings) : ControllerBase
{
    [HttpPatch(Name = "updateLearnerSettings")]
    [ProducesResponseType<LearnerResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<IdentityError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<IdentityError>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<IdentityError>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateLearnerSettings(CancellationToken cancellationToken)
    {
        if (User.GetLearnerId() is not { } learnerId)
        {
            return NotOnboarded();
        }

        LearnerSettingsRequest? request;
        try
        {
            using var document = await JsonDocument.ParseAsync(Request.Body, cancellationToken: cancellationToken);
            if (LearnerSettingsRequest.Read(document.RootElement, out request) is { } error)
            {
                return BadRequest(error);
            }
        }
        catch (JsonException)
        {
            return BadRequest(LearnerSettingsRequest.InvalidRequest());
        }

        var result = await settings.UpdateAsync(learnerId, request!, cancellationToken);
        return result.Error is { } failure ? BadRequest(failure)
            : result.Learner is { } learner ? Ok(learner) : NotOnboarded();
    }

    private NotFoundObjectResult NotOnboarded() => NotFound(new IdentityError("learner.not_onboarded", "Спершу налаштуйте тренер."));
}
