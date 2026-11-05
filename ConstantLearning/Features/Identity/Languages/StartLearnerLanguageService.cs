using ConstantLearning.Features.Content.Availability;
using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using NodaTime.Text;
using Npgsql;

namespace ConstantLearning.Features.Identity.Languages;

/// <summary>Результат спроби старту мови — гілка плюс представлення мови для 200/201 (<c>null</c> для помилок).</summary>
internal enum StartLanguageOutcome
{
    /// <summary>Перший старт: створено рядок стану й активовано мову (HTTP 201, AC-01).</summary>
    Created,

    /// <summary>Learner уже веде цю мову: стан не чіпається, повертається наявний (HTTP 200, AC-12).</summary>
    AlreadyStudying,

    /// <summary>Онбординг не завершено (HTTP 404 <c>learner.not_onboarded</c>).</summary>
    NotOnboarded,

    /// <summary>Коду немає в каталозі (HTTP 404 <c>language.unknown</c>).</summary>
    Unknown,

    /// <summary>Мова вимкнена або без схваленого контенту (HTTP 409 <c>language.not_available</c>, AC-02).</summary>
    NotAvailable,

    /// <summary>Конкурентний старт/перемикання відхилено за <c>xmin</c> (HTTP 409 <c>language.concurrent_update</c>, OQ-4).</summary>
    ConcurrentUpdate
}

internal sealed record StartLanguageResult(StartLanguageOutcome Outcome, LearnerLanguageView? Language);

/// <summary>
/// Старт цільової мови learner-ом (US-01/flow 4): створює рядок <c>learner_language_states</c> з власними
/// дефолтами (свіжа доба, стартовий ліміт з онбордингу) й робить мову активною. Мутація ідемпотентна за
/// природним ключем <c>(learner_id, target_language)</c> — повторний старт не створює другого рядка (AC-12).
/// Конкурентність — оптимістична через наявний <c>xmin</c> learner-а й PK рядка стану: конфлікт →
/// <c>language.concurrent_update</c>, клієнт повторює, без server-side retry (OQ-4).
/// </summary>
public sealed class StartLearnerLanguageService(
    AppDbContext db,
    ITargetLanguageCatalog catalog,
    IApprovedContentLanguages approvedContent,
    IClock clock)
{
    internal async Task<StartLanguageResult> StartAsync(Guid learnerId, string? targetLanguage,
        CancellationToken cancellationToken)
    {
        var learner = await db.Learners.SingleOrDefaultAsync(value => value.Id == learnerId, cancellationToken);
        if (learner is null || learner.OnboardedAt is null)
        {
            return new StartLanguageResult(StartLanguageOutcome.NotOnboarded, null);
        }

        // Порожній/невідомий код трактуємо однаково як language.unknown: контракт не визначає 400 для цієї операції.
        var code = targetLanguage?.Trim();
        if (string.IsNullOrEmpty(code) || await catalog.FindAsync(code, cancellationToken) is not { } language)
        {
            return new StartLanguageResult(StartLanguageOutcome.Unknown, null);
        }

        var existing = await db.LearnerLanguageStates.AsNoTracking()
            .SingleOrDefaultAsync(state => state.LearnerId == learnerId && state.TargetLanguage == language.Code,
                cancellationToken);
        if (existing is not null)
        {
            // Ідемпотентно (AC-12): стан не чіпаємо, активну мову не змінюємо — віддаємо наявний рядок як є.
            return new StartLanguageResult(StartLanguageOutcome.AlreadyStudying,
                ToView(language, existing, learner.ActiveTargetLanguage));
        }

        var approved = await approvedContent.ReadAsync(cancellationToken);
        if (!LanguageAvailability.IsAvailable(language.IsEnabled, approved.Contains(language.Code)))
        {
            // Активна мова лишається незмінною (AC-02): нічого не записуємо.
            return new StartLanguageResult(StartLanguageOutcome.NotAvailable, null);
        }

        // Рядок нової мови з власними дефолтами (свіжа доба — усі поля доби null, стартовий ліміт з онбордингу).
        // getToday потім ліниво ініціалізує «перший день» (ON CONFLICT DO NOTHING вже не засіває зі стану learner).
        var state = new LearnerLanguageState
        {
            LearnerId = learnerId,
            TargetLanguage = language.Code,
            DailyNewWordsLimit = learner.DailyNewWordsLimit,
            CreatedAt = clock.GetCurrentInstant()
        };
        db.LearnerLanguageStates.Add(state);
        learner.ActiveTargetLanguage = language.Code;

        try
        {
            // Вставка рядка стану й оновлення активної мови — однією транзакцією SaveChanges; xmin learner-а дає
            // оптимістичну перевірку, PK рядка стану ловить конкурентний старт тієї самої мови.
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new StartLanguageResult(StartLanguageOutcome.ConcurrentUpdate, null);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
               { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "learner_language_states_pkey" })
        {
            return new StartLanguageResult(StartLanguageOutcome.ConcurrentUpdate, null);
        }

        return new StartLanguageResult(StartLanguageOutcome.Created,
            ToView(language, state, learner.ActiveTargetLanguage));
    }

    private static LearnerLanguageView ToView(TargetLanguage language, LearnerLanguageState state,
        string? activeTargetLanguage) =>
        new(state.TargetLanguage, language.DisplayNameUk, state.TargetLanguage == activeTargetLanguage,
            InstantPattern.ExtendedIso.Format(state.CreatedAt));
}
