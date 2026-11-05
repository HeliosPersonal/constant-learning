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
    public async Task Publish_TagsFormsAndMorphologyByEntryTargetLanguage_WithEnglishAlongsidePolish()
    {
        // Польський лексикон + навмисний «витік»: pl-рядок із тією самою лемою "mouse", що й в англійського запису.
        using var lexicon = new LocalLexicon();
        await Importer.ImportAsync(lexicon.Path);
        Db.MorphForms.AddRange(
            new MorphForm { Lang = "en", Source = "test-en", Lemma = "mouse", Form = "mice" },
            new MorphForm { Lang = "en", Source = "test-en", Lemma = "café", Form = "cafés" },
            new MorphForm { Lang = "pl", Source = "test-leak", Lemma = "mouse", Form = "myszy" });
        await Db.SaveChangesAsync();

        async Task<(DictionaryEntry Entry, EntryVersion Version)> NewEnglishEntryAsync(string lemma, int coursePosition)
        {
            var entry = new DictionaryEntry
            {
                ExternalId = $"en-{lemma}-{coursePosition}", Domain = "everyday",
                TargetLanguage = "en", CoursePosition = coursePosition
            };
            var version = ReportsPublicationTestData.NewVersion(entry);
            version.LemmaPl = lemma;
            Db.Add(version);
            await Db.SaveChangesAsync();
            return (entry, version);
        }

        var mouse = await NewEnglishEntryAsync("mouse", coursePosition: 1);
        Db.Add(new VersionAcceptedAnswer { VersionId = mouse.Version.Id, TextPl = "rodent" });
        Db.Add(new VersionTranslation { VersionId = mouse.Version.Id, Kind = "canonical", TextUk = "миша", Position = 0 });
        var cafe = await NewEnglishEntryAsync("café", coursePosition: 2);
        await Db.SaveChangesAsync();

        await Publisher.ApplyAsync([
            new(mouse.Entry.Id, null, mouse.Version.Id, "editor"),
            new(cafe.Entry.Id, null, cafe.Version.Id, "editor")]);

        var forms = await Db.SearchForms.AsNoTracking().ToListAsync();
        // Форми англійського запису позначені "en"; морфологія взята з en-леми, pl-«витік» не просочився.
        forms.Where(form => form.EntryId == mouse.Entry.Id && form.Lang == "en").Select(form => form.FormDisplay)
            .Should().BeEquivalentTo("mouse", "mice", "rodent");
        forms.Should().NotContain(form => form.FormDisplay == "myszy");
        forms.Single(form => form.FormDisplay == "mouse").IsLemma.Should().BeTrue();
        // Переклад лишається українським (мова пояснень) незалежно від цільової мови запису.
        forms.Where(form => form.EntryId == mouse.Entry.Id && form.Lang == "uk").Select(form => form.FormDisplay)
            .Should().BeEquivalentTo("миша");
        // Згортання діакритики мово-свідоме для en (café→cafe, cafés→cafes) через той самий спільний fold.
        forms.Where(form => form.EntryId == cafe.Entry.Id && form.Lang == "en").Select(form => form.FormFolded)
            .Should().BeEquivalentTo("cafe", "cafes");
        (await Db.SearchForms.Where(form => form.FormFolded == SearchTextNormalizer.Fold("CAFÉ"))
            .Select(form => form.FormDisplay).ToListAsync()).Should().Contain("café");

        // Регресія pl: польський запис у тій самій БД лишається мово-свідомим і повним (узагальнення не зламало pl).
        var polish = await ReportsPublicationTestData.CreateRootAsync(Db, Clock);
        await Publisher.ApplyAsync([new(polish.Entry.Id, null, polish.Version.Id, "editor")]);
        (await Db.SearchForms.Where(form => form.EntryId == polish.Entry.Id && form.Lang == "pl")
            .Select(form => form.FormDisplay).ToListAsync()).Should().BeEquivalentTo("żaba", "żaby", "żabą");
    }

    [DockerFact]
    public async Task Publish_AppliesGermanAndDutchForms_ArticlesStrippedUmlautsExpanded()
    {
        using var lexicon = new LocalLexicon();
        await Importer.ImportAsync(lexicon.Path);
        // de-морфологія для голого іменника; nl-«витік» із тією ж лемою «hund» не має просочитися до de.
        Db.MorphForms.AddRange(
            new MorphForm { Lang = "de", Source = "test-de", Lemma = "hund", Form = "Hunde" },
            new MorphForm { Lang = "nl", Source = "test-leak", Lemma = "hund", Form = "honden" });
        await Db.SaveChangesAsync();

        async Task<(DictionaryEntry Entry, EntryVersion Version)> NewEntryAsync(string lang, string lemma, int coursePosition)
        {
            var entry = new DictionaryEntry
            {
                ExternalId = $"{lang}-{coursePosition}", Domain = "everyday",
                TargetLanguage = lang, CoursePosition = coursePosition
            };
            var version = ReportsPublicationTestData.NewVersion(entry);
            version.LemmaPl = lemma;
            Db.Add(version);
            await Db.SaveChangesAsync();
            return (entry, version);
        }

        // Німецька словникова форма з артиклем роду та ß; прийнята відповідь — голий іменник для морфології.
        var strasse = await NewEntryAsync("de", "die Straße", coursePosition: 1);
        Db.Add(new VersionAcceptedAnswer { VersionId = strasse.Version.Id, TextPl = "Hund" });
        Db.Add(new VersionTranslation { VersionId = strasse.Version.Id, Kind = "canonical", TextUk = "вулиця", Position = 0 });
        var huis = await NewEntryAsync("nl", "het huis", coursePosition: 2);
        await Db.SaveChangesAsync();

        await Publisher.ApplyAsync([
            new(strasse.Entry.Id, null, strasse.Version.Id, "editor"),
            new(huis.Entry.Id, null, huis.Version.Id, "editor")]);

        var forms = await Db.SearchForms.AsNoTracking().ToListAsync();
        var german = forms.Where(form => form.EntryId == strasse.Entry.Id && form.Lang == "de").ToList();
        // Артикль роду знято окремою безартиклевою формою; de-морфологія взята з de-леми.
        german.Select(form => form.FormDisplay).Should().Contain(["die Straße", "Straße", "Hund", "Hunde"]);
        forms.Should().NotContain(form => form.FormDisplay == "honden");
        // ß розкривається у ss; безартиклевий іменник знаходиться за folded-запитом.
        german.Single(form => form.FormDisplay == "Straße").FormFolded.Should().Be("strasse");
        german.Where(form => form.FormFolded == SearchTextNormalizer.Fold("Strasse"))
            .Select(form => form.FormDisplay).Should().Contain("Straße");
        // Лема — форма з артиклем; безартиклевий варіант не лема.
        german.Single(form => form.FormDisplay == "die Straße").IsLemma.Should().BeTrue();
        german.Single(form => form.FormDisplay == "Straße").IsLemma.Should().BeFalse();
        // Переклад лишається українським незалежно від цільової мови запису.
        forms.Where(form => form.EntryId == strasse.Entry.Id && form.Lang == "uk").Select(form => form.FormDisplay)
            .Should().BeEquivalentTo("вулиця");

        var dutch = forms.Where(form => form.EntryId == huis.Entry.Id && form.Lang == "nl").ToList();
        // Нідерландський артикль het знято; форми позначені "nl".
        dutch.Select(form => form.FormDisplay).Should().Contain(["het huis", "huis"]);
        dutch.Single(form => form.FormDisplay == "huis").FormFolded.Should().Be("huis");
        dutch.Single(form => form.FormDisplay == "het huis").IsLemma.Should().BeTrue();
        dutch.Single(form => form.FormDisplay == "huis").IsLemma.Should().BeFalse();

        // Регресія pl: польський запис у тій самій БД лишається мово-свідомим і повним.
        var polish = await ReportsPublicationTestData.CreateRootAsync(Db, Clock);
        await Publisher.ApplyAsync([new(polish.Entry.Id, null, polish.Version.Id, "editor")]);
        (await Db.SearchForms.Where(form => form.EntryId == polish.Entry.Id && form.Lang == "pl")
            .Select(form => form.FormDisplay).ToListAsync()).Should().BeEquivalentTo("żaba", "żaby", "żabą");
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
    public async Task PackagedPolimorf_InitializesEveryPublishedEntryAndRepeatsWithoutChanges()
    {
        // Пакет тепер містить усі цільові мови (pl + en/de/nl); польська морфологія та індикатор
        // «без морфології» лишаються pl-специфічними, а лічильники версій/журналу — на весь пакет.
        const int packagedTotal = 2000 + 2000 + 2000 + 2000;
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
        (await Db.EntryVersions.CountAsync()).Should().Be(packagedTotal);
        (await Db.PublicationLog.CountAsync()).Should().Be(packagedTotal);
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
