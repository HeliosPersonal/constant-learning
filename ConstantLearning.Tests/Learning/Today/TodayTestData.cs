using ConstantLearning.Features.Content.Data;
using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Features.Learning.Data;
using ConstantLearning.Platform.Persistence;
using ConstantLearning.Tests.Content.Data;
using NodaTime;

namespace ConstantLearning.Tests.Learning.Today;

internal static class TodayTestData
{
    public const long UserId = 960000001;
    public static readonly LocalDate Day = new(2026, 10, 31);

    public static Learner Learner(IClock clock, long userId = UserId) => new()
    {
        TelegramUserId = userId, TimeZone = "Europe/Warsaw", TimeZoneConfirmed = true,
        DailyNewWordsLimit = 5, OnboardedAt = clock.GetCurrentInstant(), CreatedAt = clock.GetCurrentInstant()
    };

    public static async Task<DictionaryEntry[]> CourseAsync(AppDbContext db, int count)
    {
        var versions = Enumerable.Range(1, count).Select(index =>
        {
            var entry = new DictionaryEntry { ExternalId = Guid.NewGuid().ToString("N"), Domain = "everyday", CoursePosition = index };
            var version = ReportsPublicationTestData.NewVersion(entry);
            version.FrequencyRank = index;
            return version;
        }).ToArray();
        db.EntryVersions.AddRange(versions);
        db.VersionTranslations.AddRange(versions.Select(version => new VersionTranslation
        {
            VersionId = version.Id, Kind = "canonical", TextUk = "жаба", Position = 0
        }));
        await db.SaveChangesAsync();
        foreach (var version in versions) version.Entry.PublishedVersionId = version.Id;
        await db.SaveChangesAsync();
        return versions.Select(version => version.Entry).ToArray();
    }

    public static LearnerWord Word(Learner learner, DictionaryEntry entry, string status = "review",
        int shownDaysAgo = 10, int dueDaysFromToday = 0) => new()
    {
        LearnerId = learner.Id, EntryId = entry.Id, Status = status,
        FirstShownDay = Day.PlusDays(-shownDaysAgo), DueOn = Day.PlusDays(dueDaysFromToday),
        IntervalDays = 2, ConsecutiveScheduledCorrect = status == "recalled" ? (short)3 : (short)0,
        ModelState = "{}", SchedulerVersion = "memorize-v1"
    };

    public static LearningSession Completed(Learner learner, Instant at) => new()
    {
        LearnerId = learner.Id, LearnerDay = Day.PlusDays(-20), Status = "completed", Kind = "plan",
        CompletedAt = at, EndsAt = at + Duration.FromHours(1), CreatedAt = at - Duration.FromMinutes(1)
    };

    public static AnswerEvent Attempt(Learner learner, DictionaryEntry entry, IClock clock,
        string kind = "review", string outcome = "mistake_typo", int daysAgo = 0) => new()
    {
        LearnerId = learner.Id, EntryId = entry.Id, VersionId = entry.PublishedVersionId!.Value,
        ExerciseKind = kind, Outcome = outcome, LearnerDay = Day.PlusDays(-daysAgo),
        TimeZone = learner.TimeZone, AnsweredAt = clock.GetCurrentInstant(),
        StateBefore = "{}", SchedulerVersion = "memorize-v1"
    };
}
