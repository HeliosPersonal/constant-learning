using System.Text.Json;
using ConstantLearning.Features.Identity.Auth;
using ConstantLearning.Features.Identity.Learners;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConstantLearning.Features.Identity.Languages;

[ApiController]
[Authorize]
[Route("api/v1/learners/me")]
public sealed class LearnerLanguagesController(
    ILanguageDirectory directory,
    StartLearnerLanguageService starter,
    SetActiveLanguageService switcher) : ControllerBase
{
    // Мови цього learner і активна мова; дані лише власника (AC-10) — learner визначається з initData, не з URL.
    [HttpGet("languages", Name = "listLearnerLanguages")]
    [ProducesResponseType<LearnerLanguagesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<IdentityError>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<IdentityError>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListLearnerLanguages(CancellationToken cancellationToken)
    {
        if (User.GetLearnerId() is not { } learnerId)
        {
            return NotFound(IdentityError.NotOnboarded());
        }

        var response = await directory.ListLearnerLanguagesAsync(learnerId, cancellationToken);
        return response is null ? NotFound(IdentityError.NotOnboarded()) : Ok(response);
    }

    // Старт мови (US-01): ідемпотентний за (learner, мова) — повторний старт повертає наявний стан (AC-12).
    // Невалідне/порожнє тіло трактуємо як language.unknown (404): контракт не визначає 400 для цієї операції.
    [HttpPost("languages", Name = "startLearnerLanguage")]
    [ProducesResponseType<LearnerLanguageView>(StatusCodes.Status201Created)]
    [ProducesResponseType<LearnerLanguageView>(StatusCodes.Status200OK)]
    [ProducesResponseType<IdentityError>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<IdentityError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<LanguageError>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> StartLearnerLanguage(CancellationToken cancellationToken)
    {
        if (User.GetLearnerId() is not { } learnerId)
        {
            return NotFound(IdentityError.NotOnboarded());
        }

        StartLanguageRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<StartLanguageRequest>(Request.Body,
                cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            request = null;
        }

        var result = await starter.StartAsync(learnerId, request?.TargetLanguage, cancellationToken);
        return result.Outcome switch
        {
            StartLanguageOutcome.Created => StatusCode(StatusCodes.Status201Created, result.Language),
            StartLanguageOutcome.AlreadyStudying => Ok(result.Language),
            StartLanguageOutcome.NotOnboarded => NotFound(IdentityError.NotOnboarded()),
            StartLanguageOutcome.Unknown => NotFound(LanguageErrors.Unknown()),
            StartLanguageOutcome.NotAvailable => Conflict(LanguageErrors.NotAvailable()),
            StartLanguageOutcome.ConcurrentUpdate => Conflict(LanguageErrors.ConcurrentUpdate()),
            _ => throw new InvalidOperationException($"Необроблена гілка старту мови: {result.Outcome}.")
        };
    }

    // Перемикання активної мови (US-02): лише вже вивчувану; невідомий/невивчений код → 409 language.not_studying
    // (existence-hiding, без 404). Повтор на вже активній мові безпечний (200). Невалідне тіло → теж not_studying.
    [HttpPut("active-language", Name = "setActiveLanguage")]
    [ProducesResponseType<ActiveLanguageView>(StatusCodes.Status200OK)]
    [ProducesResponseType<IdentityError>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<IdentityError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<LanguageError>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SetActiveLanguage(CancellationToken cancellationToken)
    {
        if (User.GetLearnerId() is not { } learnerId)
        {
            return NotFound(IdentityError.NotOnboarded());
        }

        SetActiveLanguageRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<SetActiveLanguageRequest>(Request.Body,
                cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            request = null;
        }

        var result = await switcher.SetActiveAsync(learnerId, request?.TargetLanguage, cancellationToken);
        return result.Outcome switch
        {
            SetActiveLanguageOutcome.Switched => Ok(new ActiveLanguageView(result.ActiveTargetLanguage!)),
            SetActiveLanguageOutcome.NotOnboarded => NotFound(IdentityError.NotOnboarded()),
            SetActiveLanguageOutcome.NotStudying => Conflict(LanguageErrors.NotStudying()),
            SetActiveLanguageOutcome.ConcurrentUpdate => Conflict(LanguageErrors.ConcurrentUpdate()),
            _ => throw new InvalidOperationException($"Необроблена гілка перемикання мови: {result.Outcome}.")
        };
    }
}
