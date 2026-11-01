using NodaTime;

namespace ConstantLearning.Tests.Fixtures;

public sealed record EntryVersionData(Guid Id, Guid EntryId, string LemmaPl, string TranslationUk,
    IReadOnlyList<string> AcceptedAnswers, string ValidationStatus);
public sealed record PublishedEntryData(Guid Id, Guid PublishedVersionId, EntryVersionData Version);
public sealed record ContentReportData(Guid Id, Guid LearnerId, Guid? EntryId, string Origin, string Status,
    Instant CreatedAt);

public static class ContentFixtures
{
    public static PublishedEntryData NewPublishedEntry(string lemmaPl = "żaba", string translationUk = "жаба",
        IEnumerable<string>? acceptedAnswers = null)
    {
        var now = LearnerFixtures.DefaultInstant.ToDateTimeOffset();
        var entryId = Guid.CreateVersion7(now);
        var versionId = Guid.CreateVersion7(now);
        return new PublishedEntryData(entryId, versionId,
            new EntryVersionData(versionId, entryId, lemmaPl, translationUk,
                Array.AsReadOnly((acceptedAnswers ?? [lemmaPl]).ToArray()), "approved"));
    }

    public static ContentReportData NewContentReport(LearnerData learner, PublishedEntryData? entry = null,
        string origin = "card", string status = "in_editor_queue", IClock? clock = null)
    {
        if (origin is not ("card" or "answer_dispute" or "missing_word"))
        {
            throw new ArgumentException("Невідоме походження content report.", nameof(origin));
        }

        var now = clock?.GetCurrentInstant() ?? LearnerFixtures.DefaultInstant;
        return new ContentReportData(Guid.CreateVersion7(now.ToDateTimeOffset()), learner.Id, entry?.Id,
            origin, status, now);
    }
}
