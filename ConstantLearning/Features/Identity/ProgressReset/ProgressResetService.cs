using System.Diagnostics;
using ConstantLearning.Features.Identity.LearnerDays;
using ConstantLearning.Platform.Persistence;
using NodaTime;

namespace ConstantLearning.Features.Identity.ProgressReset;

/// <summary>Результат скидання для контролера: мапиться на відповідь контракту.</summary>
internal enum ProgressResetOutcome
{
    /// <summary>Learner не знайдено або онбординг не завершено — 404.</summary>
    NotOnboarded,

    /// <summary>Прогрес уже порожній — 200 <c>{ erased: false }</c> (AC-09).</summary>
    AlreadyEmpty,

    /// <summary>Прогрес стерто — 200 <c>{ erased: true }</c> (AC-01).</summary>
    Erased,

    /// <summary>Скидання відкочено, прогрес без змін — 500 <c>progress_reset.failed</c> (AC-04).</summary>
    Failed
}

/// <summary>
/// Атомарне скидання прогресу learner (ADR-0001): одна транзакція під блокуванням рядка learner,
/// обробники модулів у порядку фаз, потім скидання похідних від прогресу полей learner і перевірка
/// цілісності. Налаштування й профіль не змінюються (AC-06).
/// </summary>
public sealed class ProgressResetService(AppDbContext db, ILearnerDayStateService learnerDays,
    IClock clock, IEnumerable<IProgressResetHandler> handlers, IProgressResetMetrics metrics)
{
    internal async Task<ProgressResetOutcome> ResetAsync(Guid learnerId, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var ordered = handlers.OrderBy(handler => handler.Phase).ToList();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var state = await learnerDays.GetForUpdateAsync(learnerId, cancellationToken);
        if (state is null)
        {
            return ProgressResetOutcome.NotOnboarded;
        }

        var languageState = state.State;
        var hadData = languageState.CourseCompletedAt is not null || languageState.NewWordsAvailableFrom is not null;
        foreach (var handler in ordered)
        {
            hadData |= await handler.HasPendingDataAsync(learnerId, cancellationToken);
        }

        if (!hadData)
        {
            await transaction.CommitAsync(cancellationToken);
            metrics.RecordReset(ProgressResetOutcomeKind.Empty, Stopwatch.GetElapsedTime(started));
            return ProgressResetOutcome.AlreadyEmpty;
        }

        var requested = new ProgressResetRequested(learnerId, clock.GetCurrentInstant());
        try
        {
            foreach (var handler in ordered)
            {
                await handler.EraseAsync(requested, cancellationToken);
            }

            // Два похідні від прогресу поля стану мови; решту профілю й налаштувань не чіпаємо (AC-06, AC-07).
            languageState.CourseCompletedAt = null;
            languageState.NewWordsAvailableFrom = null;
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Клієнт відʼєднався / запит скасовано — це не збій скидання. Відкат зробить dispose транзакції; не псуємо метрику збоїв.
            throw;
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            metrics.RecordReset(ProgressResetOutcomeKind.Failed, Stopwatch.GetElapsedTime(started));
            return ProgressResetOutcome.Failed;
        }

        foreach (var handler in ordered)
        {
            if (await handler.HasPendingDataAsync(learnerId, cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                metrics.RecordReset(ProgressResetOutcomeKind.IntegrityFailed, Stopwatch.GetElapsedTime(started));
                return ProgressResetOutcome.Failed;
            }
        }

        await transaction.CommitAsync(cancellationToken);
        metrics.RecordReset(ProgressResetOutcomeKind.Erased, Stopwatch.GetElapsedTime(started));
        return ProgressResetOutcome.Erased;
    }
}
