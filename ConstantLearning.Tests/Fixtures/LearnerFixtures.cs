using NodaTime;

namespace ConstantLearning.Tests.Fixtures;

// Тестові DTO адаптуються до EF-сутностей у задачах перенесення схеми.
public sealed record LearnerData(Guid Id, long TelegramUserId, string FirstName, string TimeZone,
    bool TimeZoneConfirmed, int DailyNewWordsLimit, Instant CreatedAt);

public static class LearnerFixtures
{
    public static Instant DefaultInstant { get; } = Instant.FromUtc(2026, 10, 31, 9, 0);
    private static long _telegramUserId = 900_000_000;

    public static LearnerData NewLearner(string timeZone = "Europe/Warsaw", int dailyLimit = 5, IClock? clock = null)
    {
        _ = DateTimeZoneProviders.Tzdb[timeZone];
        if (dailyLimit is not (3 or 5 or 10))
        {
            throw new ArgumentOutOfRangeException(nameof(dailyLimit), "Дозволені ліміти: 3, 5 або 10.");
        }

        var now = clock?.GetCurrentInstant() ?? DefaultInstant;
        return new LearnerData(Guid.CreateVersion7(now.ToDateTimeOffset()), Interlocked.Increment(ref _telegramUserId),
            "Test User", timeZone, true, dailyLimit, now);
    }
}
