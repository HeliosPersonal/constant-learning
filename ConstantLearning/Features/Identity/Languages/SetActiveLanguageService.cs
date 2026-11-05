using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConstantLearning.Features.Identity.Languages;

/// <summary>Результат перемикання активної мови — гілка плюс код активної мови для 200 (<c>null</c> для помилок).</summary>
internal enum SetActiveLanguageOutcome
{
    /// <summary>Активну мову перемкнено (або вона вже була активна — повтор безпечний, HTTP 200, AC-03).</summary>
    Switched,

    /// <summary>Онбординг не завершено (HTTP 404 <c>learner.not_onboarded</c>).</summary>
    NotOnboarded,

    /// <summary>Learner ще не вивчає цю мову (або код невідомий) — HTTP 409 <c>language.not_studying</c>, AC-03.</summary>
    NotStudying,

    /// <summary>Конкурентне перемикання відхилено за <c>xmin</c> (HTTP 409 <c>language.concurrent_update</c>, OQ-4).</summary>
    ConcurrentUpdate
}

internal sealed record SetActiveLanguageResult(SetActiveLanguageOutcome Outcome, string? ActiveTargetLanguage);

/// <summary>
/// Перемикання активної мови learner-а (US-02/flow 1): оновлює <c>learners.active_target_language</c>, після
/// чого «Сьогодні», заняття, словник і налаштування читаються у вимірі нової мови, а облік решти лишається
/// незмінним і доступним після зворотного перемикання (AC-03). Перемкнути можна лише вже вивчувану мову —
/// невідомий чи невивчений код дає однаковий <c>language.not_studying</c> (existence-hiding, без 404). Повтор
/// на вже активній мові безпечний. Конкурентність — оптимістична через <c>xmin</c> learner-а (OQ-4).
/// </summary>
public sealed class SetActiveLanguageService(AppDbContext db)
{
    internal async Task<SetActiveLanguageResult> SetActiveAsync(Guid learnerId, string? targetLanguage,
        CancellationToken cancellationToken)
    {
        var learner = await db.Learners.SingleOrDefaultAsync(value => value.Id == learnerId, cancellationToken);
        if (learner is null || learner.OnboardedAt is null)
        {
            return new SetActiveLanguageResult(SetActiveLanguageOutcome.NotOnboarded, null);
        }

        // Existence-hiding (AC-03): і невідомий код, і невивчена мова → той самий language.not_studying.
        // Перевіряємо лише наявність рядка стану learner-а, не торкаючись каталогу мов.
        var code = targetLanguage?.Trim();
        if (string.IsNullOrEmpty(code) || !await db.LearnerLanguageStates
                .AnyAsync(state => state.LearnerId == learnerId && state.TargetLanguage == code, cancellationToken))
        {
            return new SetActiveLanguageResult(SetActiveLanguageOutcome.NotStudying, null);
        }

        if (learner.ActiveTargetLanguage == code)
        {
            // Повтор на вже активній мові безпечний (AC-03): нічого не пишемо.
            return new SetActiveLanguageResult(SetActiveLanguageOutcome.Switched, code);
        }

        learner.ActiveTargetLanguage = code;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new SetActiveLanguageResult(SetActiveLanguageOutcome.ConcurrentUpdate, null);
        }

        return new SetActiveLanguageResult(SetActiveLanguageOutcome.Switched, code);
    }
}
