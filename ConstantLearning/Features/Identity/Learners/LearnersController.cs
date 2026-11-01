using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using ConstantLearning.Features.Identity.Auth;
using ConstantLearning.Features.Identity.Onboarding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConstantLearning.Features.Identity.Learners;

[ApiController]
[Authorize]
[Route("api/v1/learners/me")]
public sealed class LearnersController(OnboardingService onboarding) : ControllerBase
{
    [HttpGet(Name = "getCurrentLearner")]
    [ProducesResponseType<LearnerResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<IdentityError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<IdentityError>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentLearner(CancellationToken cancellationToken)
    {
        var learner = await onboarding.FindAsync(TelegramUserId(), cancellationToken);
        return learner?.OnboardedAt is null
            ? NotFound(new IdentityError("learner.not_onboarded", "Спершу налаштуйте тренер."))
            : Ok(learner);
    }

    [HttpPost("onboarding", Name = "completeOnboarding")]
    [ProducesResponseType<LearnerResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<LearnerResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<IdentityError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<IdentityError>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CompleteOnboarding(CancellationToken cancellationToken)
    {
        var telegramUserId = TelegramUserId();
        // AC-53: навіть невалідне тіло повторного запиту не повинне змінювати наявний профіль.
        if (await onboarding.FindAsync(telegramUserId, cancellationToken) is { OnboardedAt: not null } existing)
        {
            return Ok(existing);
        }

        OnboardingRequest? request;
        try
        {
            using var document = await JsonDocument.ParseAsync(Request.Body, cancellationToken: cancellationToken);
            if (OnboardingRequest.Read(document.RootElement, out request) is { } error)
            {
                return BadRequest(error);
            }
        }
        catch (JsonException)
        {
            return BadRequest(OnboardingRequest.InvalidRequest());
        }

        var result = await onboarding.CreateAsync(telegramUserId, User.FindFirstValue(ClaimTypes.GivenName), request!, cancellationToken);
        if (result.Error is { } failure)
        {
            return BadRequest(failure);
        }

        return result.Created ? StatusCode(StatusCodes.Status201Created, result.Learner) : Ok(result.Learner);
    }

    private long TelegramUserId() => long.Parse(User.FindFirstValue(TelegramAuthentication.TelegramUserIdClaim)!, CultureInfo.InvariantCulture);
}
