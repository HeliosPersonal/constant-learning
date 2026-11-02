using ConstantLearning.Features.Content.Data;
using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Features.Learning.Data;
using ConstantLearning.Platform.Persistence;
using ConstantLearning.Tests.Fixtures;
using NodaTime;

namespace ConstantLearning.Tests.Content.Data;

internal static class ReportsPublicationTestData
{
    public static async Task<Root> CreateRootAsync(AppDbContext db, IClock clock)
    {
        var learnerData = LearnerFixtures.NewLearner(clock: clock);
        var entryData = ContentFixtures.NewPublishedEntry();
        var learner = new Learner
        {
            Id = learnerData.Id,
            TelegramUserId = learnerData.TelegramUserId,
            FirstName = learnerData.FirstName,
            TimeZone = learnerData.TimeZone,
            TimeZoneConfirmed = learnerData.TimeZoneConfirmed,
            DailyNewWordsLimit = checked((short)learnerData.DailyNewWordsLimit),
            CanMessage = true
        };
        var entry = new DictionaryEntry
        {
            Id = entryData.Id,
            ExternalId = entryData.Id.ToString("N"),
            Domain = "everyday"
        };
        var version = NewVersion(entry);
        version.Id = entryData.Version.Id;
        db.AddRange(learner, version);
        await db.SaveChangesAsync();
        return new Root(learner, entry, version);
    }

    public static EntryVersion NewVersion(DictionaryEntry entry) => new()
    {
        Entry = entry,
        LemmaPl = "żaba",
        PartOfSpeech = "noun",
        PronunciationUk = "жаба",
        ExamplePl = "To jest żaba.",
        ExampleUk = "Це жаба.",
        ValidationStatus = "approved",
        CreatedByKind = "import",
        ContentHash = new string('a', 64)
    };

    public static ContentReport NewReport(Root root) => new()
    {
        LearnerId = root.Learner.Id,
        EntryId = root.Entry.Id,
        VersionId = root.Version.Id,
        Origin = "card",
        Status = "pending_verification"
    };

    public static AnswerEvent NewAnswer(Root root, Instant now) => new()
    {
        LearnerId = root.Learner.Id,
        EntryId = root.Entry.Id,
        VersionId = root.Version.Id,
        ExerciseKind = "review",
        Outcome = "mistake_typo",
        RawAnswer = "zaba",
        NormalizedAnswer = "zaba",
        AnsweredAt = now,
        LearnerDay = now.InZone(DateTimeZoneProviders.Tzdb[root.Learner.TimeZone]).Date,
        TimeZone = root.Learner.TimeZone,
        SchedulerVersion = "memorize-v1",
        StateBefore = """{"rate":0.125,"history":[true,null,2]}"""
    };

    public static ValidationEvidence NewEvidence(Guid versionId, Instant now) => new()
    {
        VersionId = versionId,
        Field = "translation",
        Source = "test-source",
        Url = "https://example.test/reference",
        Outcome = "matched",
        RetrievedAt = now
    };

    internal sealed record Root(Learner Learner, DictionaryEntry Entry, EntryVersion Version);
}
