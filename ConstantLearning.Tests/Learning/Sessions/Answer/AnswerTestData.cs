using ConstantLearning.Features.Content.Data;
using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Features.Learning.Data;
using ConstantLearning.Platform.Persistence;
using ConstantLearning.Tests.Content.Data;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace ConstantLearning.Tests.Learning.Sessions.Answer;

internal sealed record AnswerFixture(Learner Learner, DictionaryEntry[] Entries, EntryVersion[] Versions,
    LearningSession? Session, SessionItem[] Items);

internal static class AnswerTestData
{
    public const long UserId = 960000001;
    public static readonly LocalDate Day = new(2026, 10, 31);

    public static async Task<AnswerFixture> SeedAsync(AppDbContext db, IClock clock, int count = 3, bool session = true)
    {
        var learner = new Learner
        {
            TelegramUserId = UserId,
            TimeZone = "Europe/Warsaw",
            TimeZoneConfirmed = true,
            DailyNewWordsLimit = 5,
            OnboardedAt = clock.GetCurrentInstant(),
            CreatedAt = clock.GetCurrentInstant()
        };
        db.Learners.Add(learner);
        var entries = new List<DictionaryEntry>();
        var versions = new List<EntryVersion>();
        for (var index = 0; index < count; index++)
        {
            var entry = new DictionaryEntry { ExternalId = $"answer-{Guid.NewGuid():N}", Domain = "everyday", CoursePosition = index + 1 };
            var version = ReportsPublicationTestData.NewVersion(entry);
            version.LemmaPl = index == 0 ? "żaba" : $"slowo{index}";
            version.FrequencyRank = index + 1;
            entries.Add(entry);
            versions.Add(version);
            db.EntryVersions.Add(version);
            db.VersionTranslations.Add(new() { VersionId = version.Id, Kind = "canonical", TextUk = $"підказка {index}", Position = 0 });
            db.VersionAcceptedAnswers.Add(new() { VersionId = version.Id, TextPl = version.LemmaPl, IsCanonical = true });
            db.LearnerWords.Add(new LearnerWord
            {
                LearnerId = learner.Id,
                EntryId = entry.Id,
                Status = "review",
                FirstShownDay = Day.PlusDays(-10),
                DueOn = Day,
                IntervalDays = 2,
                ModelState = "{\"forgetting_rate\":0.3}",
                SchedulerVersion = "memorize-v1"
            });
        }
        await db.SaveChangesAsync();
        for (var index = 0; index < count; index++) entries[index].PublishedVersionId = versions[index].Id;
        LearningSession? learningSession = null;
        SessionItem[] items = [];
        if (session)
        {
            learningSession = new LearningSession
            {
                LearnerId = learner.Id,
                LearnerDay = Day,
                EndsAt = Instant.FromUtc(2026, 10, 31, 23, 0),
                Status = "active",
                LastActivityAt = clock.GetCurrentInstant(),
                CreatedAt = clock.GetCurrentInstant()
            };
            items = entries.Take(20).Select((entry, index) => new SessionItem
            {
                SessionId = learningSession.Id,
                EntryId = entry.Id,
                Kind = "review",
                Position = (short)(index + 1),
                State = "pending",
                CreatedAt = clock.GetCurrentInstant()
            }).ToArray();
            db.LearningSessions.Add(learningSession);
            db.SessionItems.AddRange(items);
        }
        await db.SaveChangesAsync();
        return new(learner, entries.ToArray(), versions.ToArray(), learningSession, items);
    }
}
