using System.Security.Cryptography;
using System.Text;
using ConstantLearning.Features.Content.Data;
using ConstantLearning.Features.Identity.Data;
using NodaTime;

namespace ConstantLearning.Tests.Fixtures;

public sealed record EntryVersionData(Guid Id, Guid EntryId, string LemmaPl, string TranslationUk,
    IReadOnlyList<string> AcceptedAnswers, string ValidationStatus);
public sealed record PublishedEntryData(Guid Id, Guid PublishedVersionId, EntryVersionData Version);
public sealed record ContentReportData(Guid Id, Guid LearnerId, Guid? EntryId, string Origin, string Status,
    Instant CreatedAt);

// EF-граф опублікованого запису для ізоляційних тестів (T02): сам запис, його approved-версія та
// канонічний переклад. PublishedVersionId НЕ виставлено — через цикл FK dictionary_entries ⇄ entry_versions
// викликач зберігає граф, потім лінкує published-версію й зберігає ще раз (ідіома repo, напр. PublishedCourseLanguageTests).
public sealed record PublishedEntryGraph(DictionaryEntry Entry, EntryVersion Version, VersionTranslation Translation);

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

    /// <summary>
    /// EF-граф опублікованого запису мови <paramref name="targetLanguage"/> у власному порядку
    /// <paramref name="coursePosition"/> (дискримінатор мови — ADR-0003). Викликач додає три сутності,
    /// зберігає, далі виставляє <see cref="DictionaryEntry.PublishedVersionId"/> = <c>Version.Id</c> і зберігає
    /// ще раз (цикл FK не дає зробити це одним SaveChanges). Overload розрізняється обовʼязковим
    /// <paramref name="coursePosition"/> (<see cref="int"/>) від DTO-перевантаження вище.
    /// </summary>
    public static PublishedEntryGraph NewPublishedEntry(string targetLanguage, string lemmaPl, int coursePosition,
        string translationUk = "переклад", string? externalId = null, string domain = "everyday", IClock? clock = null)
    {
        var now = clock?.GetCurrentInstant() ?? LearnerFixtures.DefaultInstant;
        var entry = new DictionaryEntry
        {
            Id = Guid.CreateVersion7(now.ToDateTimeOffset()),
            ExternalId = externalId ?? $"{targetLanguage}-{lemmaPl}-{coursePosition}",
            Domain = domain,
            TargetLanguage = targetLanguage,
            CoursePosition = coursePosition,
            CreatedAt = now
        };
        var version = new EntryVersion
        {
            Id = Guid.CreateVersion7(now.ToDateTimeOffset()),
            Entry = entry,
            LemmaPl = lemmaPl,
            PartOfSpeech = "noun",
            PronunciationUk = "слово",
            ExamplePl = $"To {lemmaPl}.",
            ExampleUk = "Це слово.",
            ValidationStatus = "approved",
            CreatedByKind = "import",
            ContentHash = FakeContentHash($"{targetLanguage}:{lemmaPl}:{coursePosition}"),
            FrequencyRank = coursePosition,
            CreatedAt = now
        };
        var translation = new VersionTranslation
        {
            VersionId = version.Id,
            Kind = "canonical",
            TextUk = translationUk,
            Position = 0,
            CreatedAt = now
        };
        return new PublishedEntryGraph(entry, version, translation);
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

    /// <summary>
    /// EF content report, прив'язаний до мови <paramref name="targetLanguage"/> (денормалізований
    /// дискримінатор — ADR-0001) і, за наявності, до запису <paramref name="entry"/>. Overload
    /// розрізняється типом першого аргументу (<see cref="Learner"/>) від DTO-перевантаження вище.
    /// Викликач додає й зберігає.
    /// </summary>
    public static ContentReport NewContentReport(Learner learner, DictionaryEntry? entry, string targetLanguage,
        string origin = "card", string status = "in_editor_queue", IClock? clock = null)
    {
        if (origin is not ("card" or "answer_dispute" or "missing_word"))
        {
            throw new ArgumentException("Невідоме походження content report.", nameof(origin));
        }

        var now = clock?.GetCurrentInstant() ?? LearnerFixtures.DefaultInstant;
        return new ContentReport
        {
            Id = Guid.CreateVersion7(now.ToDateTimeOffset()),
            LearnerId = learner.Id,
            TargetLanguage = targetLanguage,
            EntryId = entry?.Id,
            Origin = origin,
            Status = status,
            CreatedAt = now
        };
    }

    private static string FakeContentHash(string seed) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(seed))).ToLowerInvariant();
}
