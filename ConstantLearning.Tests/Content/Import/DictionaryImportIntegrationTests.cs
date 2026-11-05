using ConstantLearning.Features.Content.Data;
using ConstantLearning.Features.Content.Import;
using ConstantLearning.Features.Content.Publishing;
using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Features.Learning.Data;
using ConstantLearning.Platform.Persistence;
using ConstantLearning.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;

namespace ConstantLearning.Tests.Content.Import;

public sealed class DictionaryImportIntegrationTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;
    // Очікувані розміри випусків, що постачаються в Resources\Dictionary (pl — корінь, решта — підтеки <lang>\).
    private static readonly IReadOnlyDictionary<string, int> PackagedCourseSizes =
        new Dictionary<string, int> { ["pl"] = 2000, ["nl"] = 2000, ["de"] = 2000, ["en"] = 2000 };

    [DockerFact]
    public async Task PackagedRelease_ImportsEveryLanguagePerLanguageOrderedAndEnabled()
    {
        var total = PackagedCourseSizes.Values.Sum();
        try
        {
            var first = await Importer(Db).ImportPackagedAsync();
            var second = await Importer(Db).ImportPackagedAsync();

            first.Should().Be(new DictionaryImportResult(total, total, 0, total, 0));
            second.Should().Be(new DictionaryImportResult(0, 0, total, 0, 0));
            (await Db.EntryVersions.CountAsync()).Should().Be(total);
            (await Db.PublicationLog.CountAsync()).Should().Be(total);

            var entries = await Db.DictionaryEntries.AsNoTracking().ToListAsync();
            entries.Should().HaveCount(total);
            entries.Should().OnlyContain(entry => entry.PublishedVersionId != null && entry.Id.Version == 7);
            foreach (var (language, size) in PackagedCourseSizes)
            {
                entries.Count(entry => entry.TargetLanguage == language).Should().Be(size);
                // Ядро ADR-0003/AC-06: кожна мова впорядкована власним course_position з 1..N, без змішування мов.
                entries.Where(entry => entry.TargetLanguage == language)
                    .Select(entry => entry.CoursePosition).OrderBy(position => position)
                    .Should().Equal(Enumerable.Range(1, size).Select(value => (int?)value),
                        "мова {0} має власну наскрізну нумерацію курсу (AC-06)", language);
            }

            // Валідний імпорт зі схваленим контентом вмикає кожну нову мову (OQ-7); pl уже була ввімкнена.
            var enabled = await Db.TargetLanguages.AsNoTracking().Where(language => language.IsEnabled)
                .Select(language => language.Code).ToListAsync();
            enabled.Should().Contain(PackagedCourseSizes.Keys);

            (await Db.ValidationEvidence.CountAsync()).Should().BeGreaterThanOrEqualTo(total * 6);
            (await Db.VersionTranslations.CountAsync()).Should().BeGreaterThanOrEqualTo(total);
        }
        finally
        {
            // target_languages переживає ResetAsync — повертаємо нові мови у вимкнений стан, щоб не впливати на інші тести.
            foreach (var code in new[] { "nl", "de", "en" })
            {
                var language = await Db.TargetLanguages.SingleAsync(item => item.Code == code);
                language.IsEnabled = false;
            }
            await Db.SaveChangesAsync();
        }
    }

    [DockerFact]
    public async Task EnglishDataset_ImportsUnderTargetLanguageAndEnablesLanguage()
    {
        using var bundle = new DictionaryImportTestBundle { Language = "en" };
        bundle.Row["id"] = "editorial-water-not-a-uuid";
        bundle.Row["lemma_pl"] = "water";
        bundle.Row["gender"] = "";                       // en — рід відсутній; схвалений іменник не потребує позначки.
        bundle.Row["translation_uk"] = "вода";
        bundle.Row["accepted_answers_pl"] = "water";
        bundle.Row["pronunciation_uk"] = "вотер";
        bundle.Row["example_pl"] = "This is clean water.";
        bundle.Row["example_uk"] = "Це чиста вода.";
        bundle.Evidence["id"] = "proof-water";
        bundle.Evidence["lemma_pl"] = "water";
        bundle.Evidence["source_url"] = "https://en.wiktionary.org/wiki/water";
        bundle.Evidence["ipa"] = "ˈwɔːtər";
        bundle.Evidence["translation_candidates"] = new System.Text.Json.Nodes.JsonArray("вода", "водичка");
        bundle.Row["validation_sources"] = "proof-water";
        await bundle.WriteAsync();

        (await Db.TargetLanguages.AsNoTracking().SingleAsync(language => language.Code == "en")).IsEnabled
            .Should().BeFalse("en стартує вимкненою, доки немає валідного контенту");
        try
        {
            var result = await Importer(Db).ImportAsync(bundle.DirectoryPath);

            result.Should().Be(new DictionaryImportResult(1, 1, 0, 1, 0));
            var entry = await Db.DictionaryEntries.AsNoTracking().SingleAsync();
            entry.TargetLanguage.Should().Be("en");
            entry.PublishedVersionId.Should().NotBeNull();
            (await Db.TargetLanguages.SingleAsync(language => language.Code == "en")).IsEnabled
                .Should().BeTrue("валідний імпорт зі схваленим контентом вмикає мову (OQ-7)");
        }
        finally
        {
            // target_languages переживає ResetAsync — повертаємо en у вимкнений стан, щоб не впливати на інші тести.
            var english = await Db.TargetLanguages.SingleAsync(language => language.Code == "en");
            english.IsEnabled = false;
            await Db.SaveChangesAsync();
        }
    }

    [DockerFact]
    public Task Draft_UnpublishesAndPreservesHistory() => LossOfApprovalAsync("draft");

    [DockerFact]
    public Task NeedsReview_UnpublishesAndPreservesHistory() => LossOfApprovalAsync("needs_review");

    private async Task LossOfApprovalAsync(string status)
    {
        using var bundle = new DictionaryImportTestBundle();
        await bundle.WriteAsync();
        await Importer(Db).ImportAsync(bundle.DirectoryPath);
        var entry = await Db.DictionaryEntries.SingleAsync();
        var originalId = entry.PublishedVersionId!.Value;
        var learner = new Learner { TelegramUserId = 123456, TimeZone = "Europe/Warsaw", DailyNewWordsLimit = 5 };
        var state = new LearnerWord
        {
            LearnerId = learner.Id, EntryId = entry.Id, Status = "recalled", ModelState = "{}",
            SchedulerVersion = "test-v1", IntervalDays = 7, ConsecutiveScheduledCorrect = 2,
            DueOn = new LocalDate(2026, 11, 5)
        };
        var answer = new AnswerEvent
        {
            LearnerId = learner.Id, EntryId = entry.Id, VersionId = originalId, ExerciseKind = "review",
            Outcome = "correct", AnsweredAt = Clock.GetCurrentInstant(), LearnerDay = new LocalDate(2026, 10, 31),
            TimeZone = "Europe/Warsaw", SchedulerVersion = "test-v1", StateBefore = "{}"
        };
        Db.AddRange(learner, state, answer);
        await Db.SaveChangesAsync();
        bundle.Row["validation_status"] = status;
        await bundle.WriteAsync();

        var changed = await Importer(Db).ImportAsync(bundle.DirectoryPath);
        changed.Unpublished.Should().Be(1);
        entry.PublishedVersionId.Should().BeNull();
        entry.CoursePosition.Should().BeNull();
        (await Db.EntryVersions.CountAsync()).Should().Be(2);
        var log = await Db.PublicationLog.SingleAsync(item => item.Action == "unpublish");
        log.FromVersionId.Should().Be(originalId);
        log.ToVersionId.Should().BeNull();
        (await Db.LearnerWords.SingleAsync()).Should().BeEquivalentTo(state);
        (await Db.AnswerEvents.SingleAsync()).VersionId.Should().Be(originalId);

        (await Importer(Db).ImportAsync(bundle.DirectoryPath)).Unchanged.Should().Be(1);
        bundle.Row["validation_status"] = "approved";
        await bundle.WriteAsync();
        await Importer(Db).ImportAsync(bundle.DirectoryPath);
        entry.PublishedVersionId.Should().NotBeNull().And.NotBe(originalId);
        (await Db.EntryVersions.CountAsync()).Should().Be(3);
        (await Db.LearnerWords.SingleAsync()).Id.Should().Be(state.Id);
    }

    [DockerFact]
    public Task UnchangedOldCsv_PreservesEditorChanges() => PreservesChangesAsync("editor");

    [DockerFact]
    public Task UnchangedOldCsv_PreservesVerificationChanges() => PreservesChangesAsync("auto_verification");

    private async Task PreservesChangesAsync(string actor)
    {
        using var bundle = new DictionaryImportTestBundle();
        await bundle.WriteAsync();
        await Importer(Db).ImportAsync(bundle.DirectoryPath);
        var entry = await Db.DictionaryEntries.SingleAsync();
        var initialId = entry.PublishedVersionId!.Value;
        var edited = NewVersion(entry, actor);
        edited.BasedOnVersionId = initialId;
        Db.EntryVersions.Add(edited);
        await Db.SaveChangesAsync();
        await Publisher(Db).ApplyAsync([new(entry.Id, initialId, edited.Id, actor)]);

        (await Importer(Db).ImportAsync(bundle.DirectoryPath)).Should().Be(new DictionaryImportResult(0, 0, 1, 0, 0));
        entry.PublishedVersionId.Should().Be(edited.Id);
        (await Db.EntryVersions.CountAsync()).Should().Be(2);

        bundle.Row["example_uk"] = "Там сидить зелена жаба.";
        await bundle.WriteAsync();
        (await Importer(Db).ImportAsync(bundle.DirectoryPath)).VersionsCreated.Should().Be(1);
        var imported = await Db.EntryVersions.SingleAsync(version => version.Id == entry.PublishedVersionId);
        imported.BasedOnVersionId.Should().Be(edited.Id);
        imported.ExampleUk.Should().Be(bundle.Row["example_uk"]);
        entry.Id.Should().Be(edited.EntryId);
    }

    [DockerFact]
    public async Task SameCsv_DoesNotRestoreApprovalMutatedInDatabase()
    {
        using var bundle = new DictionaryImportTestBundle();
        await bundle.WriteAsync();
        await Importer(Db).ImportAsync(bundle.DirectoryPath);
        var entry = await Db.DictionaryEntries.SingleAsync();
        var version = await Db.EntryVersions.SingleAsync();
        await Publisher(Db).ApplyAsync([new(entry.Id, version.Id, null, "editor", Action: "unpublish")]);
        version.ValidationStatus = "needs_review";
        await Db.SaveChangesAsync();

        (await Importer(Db).ImportAsync(bundle.DirectoryPath)).Unchanged.Should().Be(1);
        entry.PublishedVersionId.Should().BeNull();
        version.ValidationStatus.Should().Be("needs_review");
    }

    [DockerFact]
    public async Task ProjectionFailure_RollsBackEntriesVersionsPointersAndLogs()
    {
        using var bundle = new DictionaryImportTestBundle();
        await bundle.WriteAsync();
        await FluentActions.Awaiting(() => Importer(Db, new FailingProjection()).ImportAsync(bundle.DirectoryPath))
            .Should().ThrowAsync<InvalidOperationException>();

        (await Db.DictionaryEntries.CountAsync()).Should().Be(0);
        (await Db.EntryVersions.CountAsync()).Should().Be(0);
        (await Db.PublicationLog.CountAsync()).Should().Be(0);
        (await Db.ValidationEvidence.CountAsync()).Should().Be(0);
    }

    [DockerFact]
    public async Task ProjectionFailure_PreservesExistingPublicationAndVersionHistory()
    {
        using var bundle = new DictionaryImportTestBundle();
        await bundle.WriteAsync();
        await Importer(Db).ImportAsync(bundle.DirectoryPath);
        var originalId = (await Db.DictionaryEntries.SingleAsync()).PublishedVersionId;
        bundle.Row["validation_status"] = "needs_review";
        await bundle.WriteAsync();

        await FluentActions.Awaiting(() => Importer(Db, new FailingProjection()).ImportAsync(bundle.DirectoryPath))
            .Should().ThrowAsync<InvalidOperationException>();

        (await Db.DictionaryEntries.SingleAsync()).PublishedVersionId.Should().Be(originalId);
        (await Db.EntryVersions.CountAsync()).Should().Be(1);
        (await Db.PublicationLog.CountAsync()).Should().Be(1);
    }

    [DockerFact]
    public async Task InitialDraft_ImportsWithoutPublishingOrInventingEvidence()
    {
        using var bundle = new DictionaryImportTestBundle();
        bundle.Row["validation_status"] = "draft";
        bundle.Row["validation_sources"] = "";
        bundle.Row["example_pl"] = "";
        bundle.Row["example_uk"] = "";
        await bundle.WriteAsync();

        var result = await Importer(Db).ImportAsync(bundle.DirectoryPath);

        result.Should().Be(new DictionaryImportResult(1, 1, 0, 0, 0));
        (await Db.DictionaryEntries.SingleAsync()).PublishedVersionId.Should().BeNull();
        (await Db.PublicationLog.CountAsync()).Should().Be(0);
        (await Db.ValidationEvidence.CountAsync()).Should().Be(0);
    }

    [DockerFact]
    public async Task ConcurrentImports_ProduceOneStableEntryVersionAndPublication()
    {
        using var bundle = new DictionaryImportTestBundle();
        await bundle.WriteAsync();
        async Task<DictionaryImportResult> RunAsync()
        {
            await using var provider = _postgres.CreateServices(Clock);
            await using var scope = provider.CreateAsyncScope();
            return await Importer(scope.ServiceProvider.GetRequiredService<AppDbContext>()).ImportAsync(bundle.DirectoryPath);
        }
        var results = await Task.WhenAll(RunAsync(), RunAsync());

        results.Sum(result => result.VersionsCreated).Should().Be(1);
        results.Sum(result => result.Unchanged).Should().Be(1);
        (await Db.DictionaryEntries.CountAsync()).Should().Be(1);
        (await Db.EntryVersions.CountAsync()).Should().Be(1);
        (await Db.PublicationLog.CountAsync()).Should().Be(1);
    }

    [DockerFact]
    public async Task Publication_RejectsStalePointerNonApprovedAndForeignVersions()
    {
        using var bundle = new DictionaryImportTestBundle();
        await bundle.WriteAsync();
        await Importer(Db).ImportAsync(bundle.DirectoryPath);
        var entry = await Db.DictionaryEntries.SingleAsync();
        var initial = entry.PublishedVersionId!.Value;
        await FluentActions.Awaiting(() => Publisher(Db).ApplyAsync([new(entry.Id, null, initial, "editor")]))
            .Should().ThrowAsync<DbUpdateConcurrencyException>();

        entry = await Db.DictionaryEntries.SingleAsync();
        var draft = NewVersion(entry, "editor");
        draft.ValidationStatus = "draft";
        Db.EntryVersions.Add(draft);
        await Db.SaveChangesAsync();
        await FluentActions.Awaiting(() => Publisher(Db).ApplyAsync([new(entry.Id, initial, draft.Id, "editor")]))
            .Should().ThrowAsync<InvalidOperationException>();
        var other = new DictionaryEntry { ExternalId = "other", Domain = "everyday" };
        var foreignVersion = NewVersion(other, "editor");
        Db.EntryVersions.Add(foreignVersion);
        await Db.SaveChangesAsync();
        await FluentActions.Awaiting(() => Publisher(Db).ApplyAsync([new(entry.Id, initial, foreignVersion.Id, "editor")]))
            .Should().ThrowAsync<InvalidOperationException>();
        (await Db.DictionaryEntries.SingleAsync(item => item.Id == entry.Id)).PublishedVersionId.Should().Be(initial);
        (await Db.PublicationLog.CountAsync()).Should().Be(1);
    }

    [DockerFact]
    public async Task Publication_UsesCallerTransactionAndSupportsRollback()
    {
        using var bundle = new DictionaryImportTestBundle();
        await bundle.WriteAsync();
        await Importer(Db).ImportAsync(bundle.DirectoryPath);
        var entry = await Db.DictionaryEntries.SingleAsync();
        var originalId = entry.PublishedVersionId!.Value;
        var edited = NewVersion(entry, "editor");
        Db.EntryVersions.Add(edited);
        await Db.SaveChangesAsync();
        await Publisher(Db).ApplyAsync([new(entry.Id, originalId, edited.Id, "editor")]);
        await using (var transaction = await Db.Database.BeginTransactionAsync())
        {
            await Publisher(Db).ApplyAsync([new(entry.Id, edited.Id, originalId, "editor", Action: "rollback")]);
            Db.Database.CurrentTransaction.Should().BeSameAs(transaction);
            await transaction.RollbackAsync();
        }
        Db.ChangeTracker.Clear();
        (await Db.DictionaryEntries.SingleAsync()).PublishedVersionId.Should().Be(edited.Id);
        (await Db.PublicationLog.CountAsync(item => item.Action == "rollback")).Should().Be(0);
        await Publisher(Db).ApplyAsync([new(entry.Id, edited.Id, originalId, "editor", Action: "rollback")]);
        (await Db.PublicationLog.SingleAsync(item => item.Action == "rollback")).FromVersionId.Should().Be(edited.Id);
    }

    private ContentPublicationService Publisher(AppDbContext db, IPublicationProjection? projection = null)
        => new(db, projection ?? new NoOpPublicationProjection(), Clock);
    private DictionaryImportService Importer(AppDbContext db, IPublicationProjection? projection = null)
        => new(db, new DictionaryImportReader(), Publisher(db, projection), Clock, NullLogger<DictionaryImportService>.Instance);
    private EntryVersion NewVersion(DictionaryEntry entry, string actor) => new()
    {
        Entry = entry, EntryId = entry.Id, LemmaPl = "żaba", PartOfSpeech = "noun", Gender = "f",
        PronunciationUk = "жаба", ExamplePl = "To jest mała żaba.", ExampleUk = "Це маленька жаба.",
        ValidationStatus = "approved", CreatedByKind = actor, ContentHash = new string('c', 64),
        FrequencyRank = 10, CreatedAt = Clock.GetCurrentInstant() + Duration.FromSeconds(1)
    };

    private sealed class FailingProjection : IPublicationProjection
    {
        public Task UpdateAsync(IReadOnlyList<PublicationChange> changes, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Тестовий збій проєкції.");
    }
}
