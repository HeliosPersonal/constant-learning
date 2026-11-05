using ConstantLearning.Features.Identity.Data;
using NodaTime;

namespace ConstantLearning.Tests.Fixtures;

// Тестові DTO адаптуються до EF-сутностей у задачах перенесення схеми.
public sealed record LearnerData(Guid Id, long TelegramUserId, string FirstName, string TimeZone,
    bool TimeZoneConfirmed, int DailyNewWordsLimit, Instant CreatedAt);

// EF-пара (learner + його стан доби для цільової мови) для ізоляційних тестів (T02).
// Навігації між Learner і LearnerLanguageState немає — тож повертаємо обидві сутності в парі,
// а викликач персистить обидві (Db.Add(both) + SaveChangesAsync).
public sealed record StudyingLearner(Learner Learner, LearnerLanguageState State);

public static class LearnerFixtures
{
    public static Instant DefaultInstant { get; } = Instant.FromUtc(2026, 10, 31, 9, 0);
    private static long _telegramUserId = 900_000_000;

    public static LearnerData NewLearner(string timeZone = "Europe/Warsaw", int dailyLimit = 5, IClock? clock = null)
    {
        _ = DateTimeZoneProviders.Tzdb[timeZone];
        if (dailyLimit is < 1 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(dailyLimit), "Дозволені ліміти: від 1 до 10.");
        }

        var now = clock?.GetCurrentInstant() ?? DefaultInstant;
        return new LearnerData(Guid.CreateVersion7(now.ToDateTimeOffset()), Interlocked.Increment(ref _telegramUserId),
            "Test User", timeZone, true, dailyLimit, now);
    }

    /// <summary>
    /// Онбордений learner, що вивчає мову <paramref name="code"/>: активна мова виставлена
    /// (<see cref="Learner.ActiveTargetLanguage"/>) і створено відповідний рядок
    /// <see cref="LearnerLanguageState"/> із незалежним денним лімітом (ADR-0002). Викликач
    /// додає обидві сутності й зберігає. PII-safe: «Test User», Telegram id від 900_000_000.
    /// </summary>
    public static StudyingLearner NewLearnerStudying(string code, short dailyLimit = 5,
        string timeZone = "Europe/Warsaw", IClock? clock = null)
    {
        _ = DateTimeZoneProviders.Tzdb[timeZone];
        if (dailyLimit is < 1 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(dailyLimit), "Дозволені ліміти: від 1 до 10.");
        }

        var now = clock?.GetCurrentInstant() ?? DefaultInstant;
        var learner = new Learner
        {
            Id = Guid.CreateVersion7(now.ToDateTimeOffset()),
            TelegramUserId = Interlocked.Increment(ref _telegramUserId),
            FirstName = "Test User",
            TimeZone = timeZone,
            TimeZoneConfirmed = true,
            DailyNewWordsLimit = dailyLimit,
            ActiveTargetLanguage = code,
            OnboardedAt = now,
            CreatedAt = now
        };
        var state = new LearnerLanguageState
        {
            LearnerId = learner.Id,
            TargetLanguage = code,
            DailyNewWordsLimit = dailyLimit,
            CreatedAt = now
        };
        return new StudyingLearner(learner, state);
    }
}
