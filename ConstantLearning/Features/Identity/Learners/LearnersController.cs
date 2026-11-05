using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using ConstantLearning.Features.Identity.Auth;
using ConstantLearning.Features.Identity.LearnerDays;
using ConstantLearning.Features.Identity.Onboarding;
using ConstantLearning.Platform.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConstantLearning.Features.Identity.Learners;

[ApiController]
[Authorize]
[Route("api/v1/learners/me")]
public sealed class LearnersController(OnboardingService onboarding, ILearnerDayStateService learnerDays, AppDbContext db) : ControllerBase
{
    [HttpGet(Name = "getCurrentLearner")]
    [ProducesResponseType<LearnerResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<IdentityError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<IdentityError>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentLearner(CancellationToken cancellationToken)
    {
        if (User.GetLearnerId() is not { } learnerId)
        {
            return NotFound(IdentityError.NotOnboarded());
        }

        return await ReadCurrentLearnerAsync(learnerId, cancellationToken);
    }

    private async Task<IActionResult> ReadCurrentLearnerAsync(Guid learnerId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var current = await learnerDays.GetForUpdateAsync(learnerId, cancellationToken);
        if (current is null)
        {
            return NotFound(IdentityError.NotOnboarded());
        }

        await transaction.CommitAsync(cancellationToken);
        return Ok(LearnerResponse.From(current.Learner));
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
            // Дозрілий ліміт уже заплановано сервером; тіло повторного запиту й далі ігнорується.
            return existing.PendingDailyNewWordsLimit is not null
                ? await ReadCurrentLearnerAsync(existing.Id, cancellationToken)
                : Ok(existing);
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
