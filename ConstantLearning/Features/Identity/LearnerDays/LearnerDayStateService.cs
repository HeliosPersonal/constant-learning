using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Features.Learning.Domain.LearnerDay;
using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace ConstantLearning.Features.Identity.LearnerDays;

internal sealed class LearnerDayStateService(AppDbContext db, ILearnerDayService days, IClock clock) : ILearnerDayStateService
{
    public Task<LearnerDayState?> GetForUpdateAsync(Guid learnerId, CancellationToken cancellationToken = default) =>
        ResolveForUpdateAsync(learnerId, targetLanguage: null, cancellationToken);

    public Task<LearnerDayState?> GetForUpdateAsync(Guid learnerId, string targetLanguage, CancellationToken cancellationToken = default) =>
        ResolveForUpdateAsync(learnerId, targetLanguage, cancellationToken);

    private async Task<LearnerDayState?> ResolveForUpdateAsync(Guid learnerId, string? targetLanguage, CancellationToken cancellationToken)
    {
        RequireTransaction();

        // Learner НЕ блокуємо: pin береться лише на рядок (learner_id, target_language), щоб мутації різних
        // мов не серіалізувалися на спільному рядку learner (ADR-0002, DoD #3). Learner читаємо задля поясу
        // й активної мови; xmin дає оптимістичну конкурентність під час дзеркалення активної мови (AppDbContext).
        var learner = await db.Learners
            .SingleOrDefaultAsync(value => value.Id == learnerId && value.OnboardedAt != null, cancellationToken);
        if (learner is null)
        {
            return null;
        }

        // TODO(T13): активна мова визначатиметься контекстом запиту (setActiveLanguage); доти — інтеримний 'pl'.
        var language = targetLanguage ?? learner.ActiveTargetLanguage ?? "pl";
        var state = await LockOrCreateStateAsync(learner, language, cancellationToken);

        // Learner читали ДО блокування рядка мови, тож його xmin міг застаріти, якби конкурентний writer тієї ж
        // мови закомітив learner між нашим читанням і записом. Перечитуємо вже ПІД блокуванням рядка мови: усі
        // writer-и активної мови серіалізуються на цьому FOR UPDATE, а неактивні мови learner не пишуть, тож
        // свіжий xmin лишається чинним до commit і дзеркало активної мови не падає на DbUpdateConcurrencyException.
        await db.Entry(learner).ReloadAsync(cancellationToken);

        var day = days.GetCurrentDay(learner.TimeZone, ReadSnapshot(state));
        if (state.PendingDailyNewWordsLimit.HasValue != state.PendingLimitEffectiveFrom.HasValue)
        {
            throw new InvalidOperationException("Pending limit і його межа повинні бути задані разом.");
        }

        if (state.PendingDailyNewWordsLimit is { } limit && state.PendingLimitEffectiveFrom <= clock.GetCurrentInstant())
        {
            state.DailyNewWordsLimit = limit;
            state.PendingDailyNewWordsLimit = null;
            state.PendingLimitEffectiveFrom = null;
        }

        StoreSnapshot(state, day);
        await db.SaveChangesAsync(cancellationToken);
        return new LearnerDayState(learner, state, day);
    }

    public LearnerDayState ChangeTimeZone(LearnerDayState current, string timeZone)
    {
        RequireTransaction();
        var resolved = days.ResolveTimeZone(timeZone);
        if (resolved.UsedFallback)
        {
            throw new ArgumentException("Потрібен відомий IANA-пояс.", nameof(timeZone));
        }

        var day = days.GetCurrentDay(resolved.Zone.Id, current.Day);
        // Пояс лишається глобальним атрибутом learner; стан доби — per-language.
        current.Learner.TimeZone = resolved.Zone.Id;
        current.Learner.TimeZoneConfirmed = true;
        StoreSnapshot(current.State, day);
        if (current.State.PendingDailyNewWordsLimit is not null)
        {
            current.State.PendingLimitEffectiveFrom = day.EndsAt;
        }

        return new LearnerDayState(current.Learner, current.State, day);
    }

    /// <summary>
    /// Lock-safe лінива ініціалізація рядка мови (AC-07): <c>FOR UPDATE</c> на неіснуючому рядку не бере
    /// блокування й повертає 0 рядків. Спершу матеріалізуємо рядок (<c>ON CONFLICT DO NOTHING</c> —
    /// ідемпотентно й безпечно до конкурентного старту), потім перечитуємо з <c>FOR UPDATE</c>, щоб рядок
    /// гарантовано існував і утримувався до commit. Існуючий рядок береться як є; це стосується і рядка,
    /// який startLearnerLanguage (T12) вже створив зі власними дефолтами (свіжа доба, стартовий ліміт) —
    /// для нього <c>ON CONFLICT DO NOTHING</c> не спрацьовує, тож засів зі стану доби learner його не чіпає.
    /// Засів зі стану learner лишається лише для історичного рядка 'pl', матеріалізованого беклфілом до T12
    /// (порожня доба learner → коректний «перший день»).
    /// </summary>
    private async Task<LearnerLanguageState> LockOrCreateStateAsync(Learner learner, string language,
        CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO learner_language_states (
                learner_id, target_language, daily_new_words_limit, pending_daily_new_words_limit,
                pending_limit_effective_from, new_words_available_from, current_day_date,
                current_day_starts_at, current_day_ends_at, current_day_original_ends_at,
                current_day_time_zone_at_start, course_completed_at)
            SELECT
                id, {language}, daily_new_words_limit, pending_daily_new_words_limit,
                pending_limit_effective_from, new_words_available_from, current_day_date,
                current_day_starts_at, current_day_ends_at, current_day_original_ends_at,
                current_day_time_zone_at_start, course_completed_at
            FROM learners WHERE id = {learner.Id}
            ON CONFLICT (learner_id, target_language) DO NOTHING
            """, cancellationToken);
        return await db.LearnerLanguageStates.FromSqlInterpolated($"""
            SELECT *, xmin FROM learner_language_states
            WHERE learner_id = {learner.Id} AND target_language = {language}
            FOR UPDATE
            """).SingleAsync(cancellationToken);
    }

    private static LearnerDayPeriod? ReadSnapshot(LearnerLanguageState state)
    {
        if (state.CurrentDayDate is null && state.CurrentDayStartsAt is null && state.CurrentDayEndsAt is null
            && state.CurrentDayOriginalEndsAt is null && state.CurrentDayTimeZoneAtStart is null)
        {
            return null;
        }

        if (state.CurrentDayDate is not { } date || state.CurrentDayStartsAt is not { } start
            || state.CurrentDayEndsAt is not { } end || state.CurrentDayOriginalEndsAt is not { } originalEnd
            || string.IsNullOrWhiteSpace(state.CurrentDayTimeZoneAtStart))
        {
            throw new InvalidOperationException("Знімок доби мови має містити всі п'ять полів або жодного.");
        }

        return new LearnerDayPeriod(date, start, end, state.CurrentDayTimeZoneAtStart, originalEnd);
    }

    private static void StoreSnapshot(LearnerLanguageState state, LearnerDayPeriod day)
    {
        state.CurrentDayDate = day.Date;
        state.CurrentDayStartsAt = day.StartsAt;
        state.CurrentDayEndsAt = day.EndsAt;
        state.CurrentDayOriginalEndsAt = day.OriginalEndsAt;
        state.CurrentDayTimeZoneAtStart = day.TimeZoneAtStart;
    }

    private void RequireTransaction()
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Сталий стан доби потребує транзакції викликувача.");
        }
    }
}
