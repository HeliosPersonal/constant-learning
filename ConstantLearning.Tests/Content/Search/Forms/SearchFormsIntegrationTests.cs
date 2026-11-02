using ConstantLearning.Features.Content;
using ConstantLearning.Features.Content.Data;
using ConstantLearning.Features.Content.Import;
using ConstantLearning.Features.Content.Publishing;
using ConstantLearning.Features.Content.Search.Forms;
using ConstantLearning.Platform.Persistence;
using ConstantLearning.Tests.Content.Data;
using ConstantLearning.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConstantLearning.Tests.Content.Search.Forms;

public sealed class SearchFormsIntegrationTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private SearchFormsProjection Projection => new(Db, Clock);
    private ContentPublicationService Publisher => new(Db, Projection, Clock);
    private PolishMorphologyImporter Importer => new(Db, Publisher, Projection, Clock, NullLogger<PolishMorphologyImporter>.Instance);
    private SearchFormsMaintenance Maintenance => new(Db, Publisher, Projection, Importer,
        new ConfigurationBuilder().Build(), NullLogger<SearchFormsMaintenance>.Instance);

    [DockerFact]
    public async Task Publish_IndexesPolishMorphologyAnswersAndOnlyStoredUkrainianTranslations()
    {
        using var lexicon = new LocalLexicon();
        await Importer.ImportAsync(lexicon.Path);
        // Навіть випадково наявний український рядок не має стати джерелом морфології.
        Db.MorphForms.Add(new MorphForm { Lang = "uk", Source = "test-source", Lemma = "жаба", Form = "жабою" });
        var root = await ReportsPublicationTestData.CreateRootAsync(Db, Clock);
        Db.AddRange(new VersionAcceptedAnswer { VersionId = root.Version.Id, TextPl = "ropucha" },
            new VersionTranslation { VersionId = root.Version.Id, Kind = "canonical", TextUk = "жаба", Position = 0 },
            new VersionTranslation { VersionId = root.Version.Id, Kind = "local", TextUk = "ґедзь", Position = 1 });
        await Db.SaveChangesAsync();

        await Publisher.ApplyAsync([new(root.Entry.Id, null, root.Version.Id, "editor")]);

        var forms = await Db.SearchForms.AsNoTracking().ToListAsync();
        forms.Should().OnlyContain(form => form.VersionId == root.Version.Id);
        forms.Where(form => form.Lang == "pl").Select(form => form.FormDisplay)
            .Should().BeEquivalentTo("żaba", "żaby", "żabą", "ropucha");
        forms.Where(form => form.Lang == "uk").Select(form => form.FormDisplay)
            .Should().BeEquivalentTo("жаба", "ґедзь");
        forms.Single(form => form.FormDisplay == "żaba").IsLemma.Should().BeTrue();
        (await Db.SearchForms.Where(form => form.FormFolded == SearchTextNormalizer.Fold("ZABA"))
            .Select(form => form.FormDisplay).ToListAsync()).Should().BeEquivalentTo("żaba", "żabą");
        (await Db.SearchForms.SingleAsync(form => form.FormFolded == "гедзь")).FormDisplay.Should().Be("ґедзь");
    }

    [DockerFact]
    public async Task Republish_ReplacesForms_UnpublishRemovesThem_WithoutTouchingHistory()
    {
        using var lexicon = new LocalLexicon();
        await Importer.ImportAsync(lexicon.Path);
        var root = await ReportsPublicationTestData.CreateRootAsync(Db, Clock);
        await Publisher.ApplyAsync([new(root.Entry.Id, null, root.Version.Id, "editor")]);
        var next = ReportsPublicationTestData.NewVersion(root.Entry);
        next.LemmaPl = "siebie";
        Db.EntryVersions.Add(next);
        await Db.SaveChangesAsync();

        await Publisher.ApplyAsync([new(root.Entry.Id, root.Version.Id, next.Id, "editor")]);
        var form = await Db.SearchForms.SingleAsync();
        form.VersionId.Should().Be(next.Id);
        form.FormDisplay.Should().Be("siebie");
        (await Maintenance.GetMissingPolishMorphologyAsync()).Should()
            .ContainSingle(value => value.EntryId == root.Entry.Id && value.LemmaPl == "siebie");

        await Publisher.ApplyAsync([new(root.Entry.Id, next.Id, null, "editor", Action: "unpublish")]);
        (await Db.SearchForms.CountAsync()).Should().Be(0);
        (await Db.EntryVersions.CountAsync()).Should().Be(2);
        (await Db.PublicationLog.CountAsync()).Should().Be(3);
    }

    [DockerFact]
    public async Task FailedProjection_RollsBackPointerLogAndDeletedAndInsertedForms()
    {
        using var lexicon = new LocalLexicon();
        await Importer.ImportAsync(lexicon.Path);
        var root = await ReportsPublicationTestData.CreateRootAsync(Db, Clock);
        await Publisher.ApplyAsync([new(root.Entry.Id, null, root.Version.Id, "editor")]);
        var originalForms = await Db.SearchForms.AsNoTracking().ToListAsync();
        var next = ReportsPublicationTestData.NewVersion(root.Entry);
        next.LemmaPl = "siebie";
        Db.EntryVersions.Add(next);
        await Db.SaveChangesAsync();
        var failing = new ContentPublicationService(Db, new FailAfterProjection(Projection, Db), Clock);

        await FluentActions.Awaiting(() => failing.ApplyAsync([new(root.Entry.Id, root.Version.Id, next.Id, "editor")]))
            .Should().ThrowAsync<InvalidOperationException>();

        (await Db.DictionaryEntries.SingleAsync()).PublishedVersionId.Should().Be(root.Version.Id);
        (await Db.PublicationLog.CountAsync()).Should().Be(1);
        (await Db.SearchForms.AsNoTracking().ToListAsync()).Should().BeEquivalentTo(originalForms);
    }

    [DockerFact]
    public async Task Backfill_RepairsExistingPublicationAndRemovesStaleFormsWithoutNewPublication()
    {
        using var lexicon = new LocalLexicon();
        await Importer.ImportAsync(lexicon.Path);
        var root = await ReportsPublicationTestData.CreateRootAsync(Db, Clock);
        var legacyPublisher = new ContentPublicationService(Db, new NoOpPublicationProjection(), Clock);
        await legacyPublisher.ApplyAsync([new(root.Entry.Id, null, root.Version.Id, "editor")]);
        (await Maintenance.RebuildAsync(onlyMissing: true)).Should().Be(1);
        (await Maintenance.RebuildAsync(onlyMissing: true)).Should().Be(0);
        (await Db.SearchForms.CountAsync()).Should().Be(3);
        (await Db.PublicationLog.CountAsync()).Should().Be(1);

        await legacyPublisher.ApplyAsync([new(root.Entry.Id, root.Version.Id, null, "editor", Action: "unpublish")]);
        await Maintenance.RebuildAsync(onlyMissing: true);
        (await Db.SearchForms.CountAsync()).Should().Be(0);
    }

    [DockerFact]
    public async Task LexiconReplacement_RebuildsPublishedFormsAndRollsBackMalformedInput()
    {
        using var first = new LocalLexicon();
        await Importer.ImportAsync(first.Path);
        var root = await ReportsPublicationTestData.CreateRootAsync(Db, Clock);
        await Publisher.ApplyAsync([new(root.Entry.Id, null, root.Version.Id, "editor")]);
        using var second = new LocalLexicon("żabie\tżaba\tsubst:sg:dat:f\n");
        (await Importer.ImportAsync(second.Path)).Changed.Should().BeTrue();
        (await Db.SearchForms.Select(form => form.FormDisplay).ToListAsync()).Should().BeEquivalentTo("żaba", "żabie");
        (await Importer.ImportAsync(second.Path)).Changed.Should().BeFalse();
        var before = await Db.MorphForms.Where(form => form.Source.StartsWith("polimorf:")).AsNoTracking().ToListAsync();
        using var invalid = new LocalLexicon("żabami\tżaba\tsubst:pl:inst:f\nпошкоджений рядок\n");

        await FluentActions.Awaiting(() => Importer.ImportAsync(invalid.Path)).Should().ThrowAsync<InvalidDataException>();
        (await Db.MorphForms.Where(form => form.Source.StartsWith("polimorf:")).AsNoTracking().ToListAsync())
            .Should().BeEquivalentTo(before);
        (await Db.SearchForms.Select(form => form.FormDisplay).ToListAsync()).Should().BeEquivalentTo("żaba", "żabie");
        await FluentActions.Awaiting(() => Importer.ImportAsync(first.Path, new string('0', 64)))
            .Should().ThrowAsync<InvalidDataException>();
    }

    [DockerFact]
    public async Task PackagedPolimorf_InitializesExistingTwoThousandEntriesAndRepeatsWithoutChanges()
    {
        var import = new DictionaryImportService(Db, new DictionaryImportReader(),
            new ContentPublicationService(Db, new NoOpPublicationProjection(), Clock), Clock,
            NullLogger<DictionaryImportService>.Instance);
        await import.ImportPackagedAsync();
        await Maintenance.InitializeAsync();
        var count = await Db.SearchForms.CountAsync();
        count.Should().BeGreaterThan(30000);
        (await Db.MorphForms.CountAsync(form => form.Source.StartsWith("polimorf:"))).Should().Be(58971);
        (await Maintenance.GetMissingPolishMorphologyAsync()).Select(value => value.LemmaPl)
            .Should().BeEquivalentTo("siebie", "związany");
        await Maintenance.InitializeAsync();
        (await Db.SearchForms.CountAsync()).Should().Be(count);
        (await Db.EntryVersions.CountAsync()).Should().Be(2000);
        (await Db.PublicationLog.CountAsync()).Should().Be(2000);
    }

    [Fact]
    public void ContentModule_RegistersRealProjectionAndMaintenance()
    {
        var descriptors = new ServiceCollection().AddContent();
        descriptors.Should().Contain(item => item.ServiceType == typeof(SearchFormsProjection));
        descriptors.Should().Contain(item => item.ServiceType == typeof(SearchFormsMaintenance));
        descriptors.Single(item => item.ServiceType == typeof(IPublicationProjection)).Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    private sealed class FailAfterProjection(SearchFormsProjection projection, AppDbContext db) : IPublicationProjection
    {
        public async Task UpdateAsync(IReadOnlyList<PublicationChange> changes, CancellationToken cancellationToken)
        {
            await projection.UpdateAsync(changes, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("Тестовий збій після запису пошукових форм.");
        }
    }

    private sealed class LocalLexicon : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".tab");
        public LocalLexicon(string text = "żaba\tżaba\tsubst:sg:nom:f\nżaby\tżaba\tsubst:sg:gen:f\nżabą\tżaba\tsubst:sg:inst:f\n")
            => File.WriteAllText(Path, text);
        public void Dispose() => File.Delete(Path);
    }
}
