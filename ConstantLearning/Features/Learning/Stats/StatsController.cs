using ConstantLearning.Features.Identity.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConstantLearning.Features.Learning.Stats;

/// <summary>
/// Екран «Мої слова» (learned-words-stats): список вивчених слів, картка слова й зведення.
/// Лише читання; слабкі слова тренуються наявним POST /api/v1/sessions/extra-practice.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/stats")]
public sealed class StatsController(LearnedWordsStatsService stats) : ControllerBase
{
    // [Authorize] вже відкидає запити без валідного tma (401 до екшена). Тож тут порожній learner_id
    // означає автентифікований, але не онбордений користувач — list/summary → 404 not_onboarded,
    // а картка приховує існування тим самим кодом, що й невідоме слово (stats.word_not_found).
    [HttpGet("words", Name = "listLearnedWords")]
    public async Task<IActionResult> ListWords(CancellationToken cancellationToken)
    {
        if (User.GetLearnerId() is not { } learnerId) return NotOnboarded();
        if (LearnedWordsStatsRequest.Read(Request.Query, out var request) is { } error) return BadRequest(error);
        var page = await stats.ListAsync(learnerId, request!, cancellationToken);
        return page is null ? NotOnboarded() : Ok(page);
    }

    [HttpGet("words/{entry_id}", Name = "getLearnedWordCard")]
    public async Task<IActionResult> GetWord(string entry_id, CancellationToken cancellationToken)
    {
        if (User.GetLearnerId() is not { } learnerId) return WordNotFound();
        if (!Guid.TryParseExact(entry_id, "D", out var entryId)) return WordNotFound();
        var card = await stats.GetCardAsync(learnerId, entryId, cancellationToken);
        return card is null ? WordNotFound() : Ok(card);
    }

    [HttpGet("summary", Name = "getLearnedWordsSummary")]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
        if (User.GetLearnerId() is not { } learnerId) return NotOnboarded();
        var summary = await stats.SummaryAsync(learnerId, cancellationToken);
        return summary is null ? NotOnboarded() : Ok(summary);
    }

    private NotFoundObjectResult NotOnboarded() =>
        NotFound(new StatsError("learner.not_onboarded", "Спершу налаштуйте тренер."));

    private NotFoundObjectResult WordNotFound() =>
        NotFound(new StatsError("stats.word_not_found", "Слово ще не вивчалося."));
}
