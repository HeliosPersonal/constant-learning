using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using ConstantLearning.Features.Content.Data;
using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Features.Learning.Data;
using ConstantLearning.Tests.Fixtures;
using ConstantLearning.Tests.Infrastructure;
using ConstantLearning.Tests.Learning.Today;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using static ConstantLearning.Tests.Learning.Today.TodayTestData;

namespace ConstantLearning.Tests.Learning.Isolation;

// T17 (AC-07) — лічильники /api/v1/today виключають інші мови. TodayService звужує читання за активною мовою
// learner двічі: денормалізованою колонкою target_language (слова/відповіді/сесії) і словником опублікованого
// course тієї ж мови (published.ContainsKey). Тест засіває прогрес обох мов одному learner і доводить, що
// Today показує лише активну мову, а перемикання активної мови перекидає набір — 0 витоків між мовами.
// Доповнює ProgressIsolationTests (T10), який не торкався Today-ендпоінта.
public sealed class TodayLanguageIsolationTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;
    private const string Path = "/api/v1/today";

    [DockerFact]
    public async Task Ac07_TodayCountsReflectOnlyActiveLanguageAndFlipOnSwitch()
    {
        var learner = Learner(Clock);
        learner.ActiveTargetLanguage = "pl";
        Db.Add(learner);

        // pl: 3 до повторення + 2 recalled + 1 завершена сесія за останні 7 днів.
        var pl = await CourseAsync(Db, 5);
        Db.LearnerWords.AddRange(pl.Take(3).Select(entry => Word(learner, entry)));
        Db.LearnerWords.AddRange(pl.Skip(3).Take(2).Select(entry => Word(learner, entry, "recalled", 7, 1)));
        Db.LearningSessions.Add(Completed(learner, Clock.GetCurrentInstant()));

        // en: 2 до повторення + 1 recalled + 2 завершені сесії — свідомо інші числа, щоб витік мови впав тестом.
        var en = await SeedEnCourseAsync(3);
        Db.LearnerWords.AddRange(en.Take(2).Select(entry => EnWord(learner, entry)));
        Db.LearnerWords.Add(EnWord(learner, en[2], "recalled", 7, 1));
        Db.LearningSessions.Add(EnCompleted(learner, Clock.GetCurrentInstant()));
        Db.LearningSessions.Add(EnCompleted(learner, Clock.GetCurrentInstant() - Duration.FromHours(2)));
        await Db.SaveChangesAsync();

        await using var app = await StartAsync();
        AssertCounts(await GetAsync(app), due: 3, recalled: 2, completed: 1);

        await SwitchActiveLanguageAsync("en");
        AssertCounts(await GetAsync(app), due: 2, recalled: 1, completed: 2);

        // Назад на pl — набір знову pl, жоден en-рядок не протік (ізоляція симетрична).
        await SwitchActiveLanguageAsync("pl");
        AssertCounts(await GetAsync(app), due: 3, recalled: 2, completed: 1);
    }

    private LearnerWord EnWord(Learner learner, DictionaryEntry entry, string status = "review",
        int shownDaysAgo = 10, int dueDaysFromToday = 0)
    {
        var word = Word(learner, entry, status, shownDaysAgo, dueDaysFromToday);
        word.TargetLanguage = "en";
        return word;
    }

    private static LearningSession EnCompleted(Learner learner, Instant at)
    {
        var session = Completed(learner, at);
        session.TargetLanguage = "en";
        return session;
    }

    private async Task<DictionaryEntry[]> SeedEnCourseAsync(int count)
    {
        var graphs = Enumerable.Range(1, count)
            .Select(index => ContentFixtures.NewPublishedEntry("en", $"wort{index}", index, clock: Clock)).ToArray();
        foreach (var graph in graphs)
        {
            Db.Add(graph.Entry);
            Db.Add(graph.Version);
            Db.Add(graph.Translation);
        }
        await Db.SaveChangesAsync();
        foreach (var graph in graphs) graph.Entry.PublishedVersionId = graph.Version.Id;
        await Db.SaveChangesAsync();
        return graphs.Select(graph => graph.Entry).ToArray();
    }

    private async Task SwitchActiveLanguageAsync(string language)
    {
        Db.ChangeTracker.Clear();
        var learner = await Db.Learners.SingleAsync(value => value.TelegramUserId == UserId);
        learner.ActiveTargetLanguage = language;
        await Db.SaveChangesAsync();
    }

    private async Task<TodayTestApplication> StartAsync()
    {
        var app = await TodayTestApplication.StartAsync(_postgres.ConnectionString, Clock);
        app.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("tma",
            new TelegramInitDataSigner().Sign(UserId, Clock.GetCurrentInstant()));
        return app;
    }

    private async Task<JsonNode> GetAsync(TodayTestApplication app)
    {
        using var response = await app.Client.GetAsync(Path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractAssert.ResponseAsync("getToday", response);
        return JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
    }

    private static void AssertCounts(JsonNode result, int due, int recalled, int completed)
    {
        Assert.Equal(due, result["due_review_count"]!.GetValue<int>());
        Assert.Equal(recalled, result["recalled_words_count"]!.GetValue<int>());
        Assert.Equal(completed, result["completed_sessions_last_7_days"]!.GetValue<int>());
    }
}
