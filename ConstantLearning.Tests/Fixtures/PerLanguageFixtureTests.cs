using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Tests.Infrastructure;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;

namespace ConstantLearning.Tests.Fixtures;

/// <summary>
/// T02 DoD: per-language EF-фабрики компілюються й застосовані мінімум в одному smoke-тесті на кожну
/// (learner, entry, report) поверх реального PostgreSQL-testcontainer (без мокнутого DbContext), з
/// інжектованим фіксованим годинником. Коди мов (pl/en/…) переживають reset (seed target_languages),
/// тож фабрики ними лише користуються.
/// </summary>
public sealed class PerLanguageFixtureTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    [DockerFact]
    public async Task NewLearnerStudying_PersistsLearnerWithActiveLanguageAndStateRow()
    {
        var studying = LearnerFixtures.NewLearnerStudying("en", dailyLimit: 7, clock: Clock);
        Db.Add(studying.Learner);
        Db.Add(studying.State);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();

        var learner = await Db.Learners.AsNoTracking().SingleAsync(l => l.Id == studying.Learner.Id);
        learner.ActiveTargetLanguage.Should().Be("en");
        learner.FirstName.Should().Be("Test User");
        learner.TelegramUserId.Should().BeGreaterThanOrEqualTo(900_000_000);
        learner.OnboardedAt.Should().Be(Clock.GetCurrentInstant());
        learner.CreatedAt.Should().Be(Clock.GetCurrentInstant());

        var state = await Db.Set<LearnerLanguageState>().AsNoTracking()
            .SingleAsync(s => s.LearnerId == studying.Learner.Id && s.TargetLanguage == "en");
        state.DailyNewWordsLimit.Should().Be(7);
    }

    [DockerFact]
    public async Task NewPublishedEntry_PersistsPublishedEntryScopedToLanguage()
    {
        var graph = ContentFixtures.NewPublishedEntry("en", "cat", coursePosition: 1, clock: Clock);
        Db.Add(graph.Entry);
        Db.Add(graph.Version);
        Db.Add(graph.Translation);
        await Db.SaveChangesAsync();
        graph.Entry.PublishedVersionId = graph.Version.Id; // лінк published-версії після першого save (цикл FK)
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();

        var entry = await Db.DictionaryEntries.AsNoTracking().SingleAsync(e => e.Id == graph.Entry.Id);
        entry.TargetLanguage.Should().Be("en");
        entry.CoursePosition.Should().Be(1);
        entry.PublishedVersionId.Should().Be(graph.Version.Id);

        var translation = await Db.VersionTranslations.AsNoTracking()
            .SingleAsync(t => t.VersionId == graph.Version.Id);
        translation.Kind.Should().Be("canonical");
    }

    [DockerFact]
    public async Task NewContentReport_PersistsReportBoundToLanguageAndEntry()
    {
        var studying = LearnerFixtures.NewLearnerStudying("en", clock: Clock);
        var graph = ContentFixtures.NewPublishedEntry("en", "dog", coursePosition: 2, clock: Clock);
        Db.Add(studying.Learner);
        Db.Add(studying.State);
        Db.Add(graph.Entry);
        Db.Add(graph.Version);
        Db.Add(graph.Translation);
        await Db.SaveChangesAsync();

        var report = ContentFixtures.NewContentReport(studying.Learner, graph.Entry, "en", clock: Clock);
        Db.Add(report);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();

        var persisted = await Db.ContentReports.AsNoTracking().SingleAsync(r => r.Id == report.Id);
        persisted.LearnerId.Should().Be(studying.Learner.Id);
        persisted.EntryId.Should().Be(graph.Entry.Id);
        persisted.TargetLanguage.Should().Be("en");
        persisted.Origin.Should().Be("card");
        persisted.Status.Should().Be("in_editor_queue");
    }

    [DockerFact]
    public async Task TwoLearnersTwoLanguages_LanguageStateRowsAreIsolated()
    {
        var polish = LearnerFixtures.NewLearnerStudying("pl", dailyLimit: 3, clock: Clock);
        var english = LearnerFixtures.NewLearnerStudying("en", dailyLimit: 9, clock: Clock);
        Db.Add(polish.Learner);
        Db.Add(polish.State);
        Db.Add(english.Learner);
        Db.Add(english.State);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();

        var states = await Db.Set<LearnerLanguageState>().AsNoTracking()
            .Where(s => s.LearnerId == polish.Learner.Id || s.LearnerId == english.Learner.Id)
            .ToListAsync();
        states.Should().HaveCount(2);
        states.Should().ContainSingle(s => s.LearnerId == polish.Learner.Id && s.TargetLanguage == "pl"
            && s.DailyNewWordsLimit == 3);
        states.Should().ContainSingle(s => s.LearnerId == english.Learner.Id && s.TargetLanguage == "en"
            && s.DailyNewWordsLimit == 9);
        polish.Learner.TelegramUserId.Should().NotBe(english.Learner.TelegramUserId);
    }
}
