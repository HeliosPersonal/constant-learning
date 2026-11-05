using ConstantLearning.Features.Content.Availability;
using ConstantLearning.Features.Content.Course;
using ConstantLearning.Features.Content.Data;
using ConstantLearning.Tests.Infrastructure;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ConstantLearning.Tests.Content.Course;

/// <summary>
/// T06 DoD: base course добирається на мову у власному порядку <c>course_position</c> (AC-01/AC-06) і
/// прапорець доступності мови враховує лише approved-контент тієї мови (AC-02). Перевіряємо, що мова B
/// не протікає в добір мови A навіть за однакових <c>course_position</c>, і що мова без схваленого
/// опублікованого слова відсутня у наборі доступних мов.
/// </summary>
public sealed class PublishedCourseLanguageTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    [DockerFact]
    public async Task ReadByLanguage_TakesOnlyThatLanguageInCoursePositionOrder_WithoutLeakFromOther()
    {
        // Обидві мови мають записи на тих самих course_position 1 і 2 — чистий тест ізоляції (AC-06):
        // завершення/добір однієї мови не може зсунути добір іншої, бо вони читаються окремими наборами.
        var polishOne = PublishEntry("pl-kot", "pl", position: 1, lemma: "kot");
        var polishTwo = PublishEntry("pl-pies", "pl", position: 2, lemma: "pies");
        var englishOne = PublishEntry("en-cat", "en", position: 1, lemma: "cat");
        var englishTwo = PublishEntry("en-dog", "en", position: 2, lemma: "dog");
        await Db.SaveChangesAsync();
        foreach (var (entry, versionId) in new[] { polishOne, polishTwo, englishOne, englishTwo })
            entry.PublishedVersionId = versionId;
        await Db.SaveChangesAsync();

        await using var provider = new ServiceCollection().AddSingleton(Db).AddPublishedCourseReader().BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IPublishedCourseReader>();

        var polish = await reader.ReadAsync("pl");
        polish.Select(word => word.EntryId).Should().BeEquivalentTo([polishOne.Entry.Id, polishTwo.Entry.Id]);
        polish.OrderBy(word => word.CoursePosition).Select(word => word.LemmaPl).Should().Equal("kot", "pies");

        var english = await reader.ReadAsync("en");
        english.Select(word => word.EntryId).Should().BeEquivalentTo([englishOne.Entry.Id, englishTwo.Entry.Id]);
        english.OrderBy(word => word.CoursePosition).Select(word => word.LemmaPl).Should().Equal("cat", "dog");

        // Безаргументний overload лишається глобальним (lookup/прогрів) — бачить обидві мови.
        (await reader.ReadAsync()).Should().HaveCount(4);
    }

    [DockerFact]
    public async Task ApprovedContentLanguages_OmitsLanguageWithoutApprovedPublishedContent()
    {
        // pl: схвалене опубліковане слово → доступне. de: версія існує, але не опублікована → недоступне (AC-02).
        var polish = PublishEntry("pl-kot", "pl", position: 1, lemma: "kot");
        var german = PublishEntry("de-hund", "de", position: 1, lemma: "Hund");
        await Db.SaveChangesAsync();
        polish.Entry.PublishedVersionId = polish.VersionId; // de лишається без PublishedVersionId
        await Db.SaveChangesAsync();

        await using var provider = new ServiceCollection().AddSingleton(Db).AddContentAvailability().BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var languages = await scope.ServiceProvider.GetRequiredService<IApprovedContentLanguages>().ReadAsync();

        languages.Should().Contain("pl");
        languages.Should().NotContain("de");
    }

    private (DictionaryEntry Entry, Guid VersionId) PublishEntry(string externalId, string targetLanguage, int position, string lemma)
    {
        var entry = new DictionaryEntry
        {
            ExternalId = externalId,
            Domain = "everyday",
            CoursePosition = position,
            TargetLanguage = targetLanguage
        };
        var version = new EntryVersion
        {
            Entry = entry,
            LemmaPl = lemma,
            PartOfSpeech = "noun",
            PronunciationUk = "слово",
            ExamplePl = "To słowo.",
            ExampleUk = "Це слово.",
            ValidationStatus = "approved",
            CreatedByKind = "import",
            ContentHash = new string('a', 64),
            FrequencyRank = position
        };
        Db.EntryVersions.Add(version);
        Db.VersionTranslations.Add(new VersionTranslation { VersionId = version.Id, Kind = "canonical", TextUk = "переклад", Position = 0 });
        return (entry, version.Id);
    }
}
