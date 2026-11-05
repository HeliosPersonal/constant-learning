using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using ConstantLearning.Features.Content.Data;
using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Features.Learning.Data;
using ConstantLearning.Tests.Content.Data;
using ConstantLearning.Tests.Infrastructure;
using ConstantLearning.Tests.Learning.Sessions.Start;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace ConstantLearning.Tests.Learning.Stats;

/// <summary>
/// Екран «Мої слова» (learned-words-stats): підрахунок показ/правильно/неправильно за зарахованими
/// видами вправ з вирахуванням успішно оскаржених помилок, вкладки за статусом learner_words,
/// курсорна пагінація + сортування + пошук, картка слова з розбивкою помилок, зведення та авторизація.
/// Доба learner — Europe/Warsaw, тож UTC-межа доби — 23:00.
/// </summary>
public sealed class LearnedWordsStatsTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    [Fact]
    public async Task ActiveLanguageExcludesWordsFromOtherLanguages()
    {
        var learner = await SeedLearnerAsync();
        var course = await SeedCourseAsync(1);
        AddLearnerWord(learner, course[0], "review", firstShown: Day.PlusDays(-1), dueOn: Day.PlusDays(1));
        learner.ActiveTargetLanguage = "en";
        await Db.SaveChangesAsync();
        await using var app = await StartAsync();
        using var list = await app.Client.GetAsync(WordsPath);
        Assert.Empty((await BodyAsync(list))["items"]!.AsArray());
        using var summary = await app.Client.GetAsync(SummaryPath);
        Assert.Equal(0, (await BodyAsync(summary))["seen_count"]!.GetValue<int>());
        using var card = await app.Client.GetAsync($"{WordsPath}/{course[0].Id}");
        Assert.Equal(HttpStatusCode.NotFound, card.StatusCode);
    }

    private readonly PostgresFixture _postgres = postgres;
    private const long UserId = 950000001;
    private const string WordsPath = "/api/v1/stats/words";
    private const string SummaryPath = "/api/v1/stats/summary";
    private static readonly LocalDate Day = new(2026, 10, 31);

    [DockerFact]
    public async Task CountsShownCorrectIncorrectOverGradedExercisesAndExcludesDisputedMistakes()
    {
        var learner = await SeedLearnerAsync();
        var course = await SeedCourseAsync(2);
        AddLearnerWord(learner, course[0], "review", firstShown: Day.PlusDays(-5), dueOn: Day.PlusDays(1));
        // Два правильних показ + одна помилка-друкарка, яку згодом успішно оскаржили (нейтралізовано).
        AddAnswer(learner, course[0], "correct");
        AddAnswer(learner, course[0], "correct", kind: "control_repetition");
        var disputed = AddAnswer(learner, course[0], "mistake_typo");
        // Друкарка без оскарження — рахується як помилка.
        AddAnswer(learner, course[0], "mistake_typo", kind: "extra_practice");
        // Вид вправи поза зарахованими (new_word_card) не враховується у статистику зовсім.
        AddAnswer(learner, course[0], "correct", kind: "new_word_card");
        await Db.SaveChangesAsync();
        var report = new ContentReport
        {
            LearnerId = learner.Id,
            EntryId = course[0].Id,
            AnswerEventId = disputed.Id,
            Origin = "card",
            Status = "verified"
        };
        Db.Add(report);
        await Db.SaveChangesAsync();
        Db.AnswerNeutralizations.Add(new AnswerNeutralization
        {
            AnswerEventId = disputed.Id,
            ContentReportId = report.Id,
            CreatedAt = Clock.GetCurrentInstant()
        });
        await Db.SaveChangesAsync();

        await using var app = await StartAsync();
        using var response = await app.Client.GetAsync($"{WordsPath}?tab=all");
        await AssertResponseAsync(response, "listLearnedWords", HttpStatusCode.OK);
        var item = (await BodyAsync(response))["items"]!.AsArray()
            .Single(node => node!["entry_id"]!.GetValue<Guid>() == course[0].Id)!;
        // Показ рахує будь-яку зараховану спробу (4: два correct + дві друкарки); нейтралізована друкарка
        // не входить у incorrect, тож неправильних лишається одна.
        Assert.Equal(4, item["shown_count"]!.GetValue<int>());
        Assert.Equal(2, item["correct_count"]!.GetValue<int>());
        Assert.Equal(1, item["incorrect_count"]!.GetValue<int>());
    }

    [DockerFact]
    public async Task TabsSelectWordsByLearnerWordStatus()
    {
        var learner = await SeedLearnerAsync();
        var course = await SeedCourseAsync(3);
        AddLearnerWord(learner, course[0], "review", firstShown: Day.PlusDays(-5), dueOn: Day.PlusDays(1));
        AddLearnerWord(learner, course[1], "recalled", firstShown: Day.PlusDays(-6), dueOn: Day.PlusDays(2));
        // not_started слова ще не вивчалися — у статистику не потрапляють на жодній вкладці.
        AddLearnerWord(learner, course[2], "not_started", firstShown: Day.PlusDays(-1), dueOn: Day.PlusDays(1));
        await Db.SaveChangesAsync();

        await using var app = await StartAsync();

        Assert.Equal(new[] { course[0].Id, course[1].Id }.OrderBy(id => id),
            (await EntryIdsAsync(app, $"{WordsPath}?tab=all")).OrderBy(id => id));
        Assert.Equal(new[] { course[0].Id }, await EntryIdsAsync(app, $"{WordsPath}?tab=review"));
        Assert.Equal(new[] { course[1].Id }, await EntryIdsAsync(app, $"{WordsPath}?tab=recalled"));
    }

    [DockerFact]
    public async Task SortAlphaOrdersByLemmaAndWeakestLiftsForgottenWords()
    {
        var learner = await SeedLearnerAsync();
        var course = await SeedCourseAsync(3);
        SetLemma(course[0], "zupa");
        SetLemma(course[1], "auto");
        SetLemma(course[2], "mleko");
        await Db.SaveChangesAsync();
        AddLearnerWord(learner, course[0], "review", firstShown: Day.PlusDays(-5), dueOn: Day.PlusDays(1), lapses: 0);
        AddLearnerWord(learner, course[1], "review", firstShown: Day.PlusDays(-5), dueOn: Day.PlusDays(2), lapses: 0);
        AddLearnerWord(learner, course[2], "review", firstShown: Day.PlusDays(-5), dueOn: Day.PlusDays(3), lapses: 3);
        await Db.SaveChangesAsync();

        await using var app = await StartAsync();
        // alpha: auto, mleko, zupa
        Assert.Equal(new[] { course[1].Id, course[2].Id, course[0].Id },
            await EntryIdsAsync(app, $"{WordsPath}?sort=alpha"));
        // weakest: слово з найбільшою кількістю забувань (lapses) першим.
        Assert.Equal(course[2].Id, (await EntryIdsAsync(app, $"{WordsPath}?sort=weakest"))[0]);
    }

    [DockerFact]
    public async Task SearchFoldsQueryAgainstLemmaAndTranslation()
    {
        var learner = await SeedLearnerAsync();
        var course = await SeedCourseAsync(2);
        SetLemma(course[0], "chata");
        SetTranslation(course[0], "хата");
        AddTranslation(course[0], "домівка"); // другорядний переклад, який теж показано на картці
        SetLemma(course[1], "auto");
        SetTranslation(course[1], "авто");
        await Db.SaveChangesAsync();
        AddLearnerWord(learner, course[0], "review", firstShown: Day.PlusDays(-5), dueOn: Day.PlusDays(1));
        AddLearnerWord(learner, course[1], "review", firstShown: Day.PlusDays(-5), dueOn: Day.PlusDays(2));
        await Db.SaveChangesAsync();

        await using var app = await StartAsync();
        Assert.Equal(new[] { course[0].Id }, await EntryIdsAsync(app, $"{WordsPath}?q=chata"));
        Assert.Equal(new[] { course[0].Id }, await EntryIdsAsync(app, $"{WordsPath}?q=хата"));
        // Пошук за другорядним перекладом теж знаходить слово (не лише за канонічним).
        Assert.Equal(new[] { course[0].Id }, await EntryIdsAsync(app, $"{WordsPath}?q=домівка"));
    }

    [DockerFact]
    public async Task CursorPaginationWalksTheOrderedListWithoutGapsOrDuplicates()
    {
        var learner = await SeedLearnerAsync();
        var course = await SeedCourseAsync(5);
        for (var index = 0; index < 5; index++)
            AddLearnerWord(learner, course[index], "review",
                firstShown: Day.PlusDays(-10 + index), dueOn: Day.PlusDays(index + 1));
        await Db.SaveChangesAsync();

        await using var app = await StartAsync();
        using var first = await app.Client.GetAsync($"{WordsPath}?sort=alpha&limit=2");
        await AssertResponseAsync(first, "listLearnedWords", HttpStatusCode.OK);
        var firstBody = await BodyAsync(first);
        Assert.True(firstBody["has_next"]!.GetValue<bool>());
        Assert.False(firstBody["has_prev"]!.GetValue<bool>());
        var cursor = firstBody["next_cursor"]!.GetValue<Guid>();
        var firstIds = firstBody["items"]!.AsArray().Select(node => node!["entry_id"]!.GetValue<Guid>()).ToArray();
        Assert.Equal(2, firstIds.Length);

        using var second = await app.Client.GetAsync($"{WordsPath}?sort=alpha&limit=2&after={cursor:D}");
        await AssertResponseAsync(second, "listLearnedWords", HttpStatusCode.OK);
        var secondIds = (await BodyAsync(second))["items"]!.AsArray()
            .Select(node => node!["entry_id"]!.GetValue<Guid>()).ToArray();
        Assert.Equal(2, secondIds.Length);
        Assert.Empty(firstIds.Intersect(secondIds));
    }

    [DockerFact]
    public async Task CardReturnsMistakeBreakdownAndTranslations()
    {
        var learner = await SeedLearnerAsync();
        var course = await SeedCourseAsync(1);
        AddLearnerWord(learner, course[0], "review", firstShown: Day.PlusDays(-5), dueOn: Day.PlusDays(1));
        AddAnswer(learner, course[0], "correct");
        AddAnswer(learner, course[0], "mistake_typo");
        AddAnswer(learner, course[0], "mistake_diacritic");
        AddAnswer(learner, course[0], "dont_know");
        await Db.SaveChangesAsync();

        await using var app = await StartAsync();
        using var response = await app.Client.GetAsync($"{WordsPath}/{course[0].Id:D}");
        await AssertResponseAsync(response, "getLearnedWordCard", HttpStatusCode.OK);
        var card = await BodyAsync(response);
        var breakdown = card["mistake_breakdown"]!;
        Assert.Equal(1, breakdown["typo"]!.GetValue<int>());
        Assert.Equal(1, breakdown["diacritic"]!.GetValue<int>());
        Assert.Equal(1, breakdown["dont_know"]!.GetValue<int>());
        Assert.Equal(3, card["incorrect_count"]!.GetValue<int>());
        Assert.NotEmpty(card["translations_uk"]!.AsArray());
    }

    [DockerFact]
    public async Task CardHidesWordsNeverSeenBehind404()
    {
        var learner = await SeedLearnerAsync();
        var course = await SeedCourseAsync(2);
        AddLearnerWord(learner, course[0], "review", firstShown: Day.PlusDays(-5), dueOn: Day.PlusDays(1));
        await Db.SaveChangesAsync();

        await using var app = await StartAsync();
        // course[1] ще не вивчалося: існування приховано однаковим кодом, що й невалідний entry_id.
        using var unseen = await app.Client.GetAsync($"{WordsPath}/{course[1].Id:D}");
        await AssertResponseAsync(unseen, "getLearnedWordCard", HttpStatusCode.NotFound);
        Assert.Equal("stats.word_not_found", (await BodyAsync(unseen))["code"]!.GetValue<string>());
    }

    [DockerFact]
    public async Task SummaryCountsSeenRecalledReviewAndWeak()
    {
        var learner = await SeedLearnerAsync();
        var course = await SeedCourseAsync(3);
        AddLearnerWord(learner, course[0], "review", firstShown: Day.PlusDays(-5), dueOn: Day.PlusDays(1), lapses: 2);
        AddLearnerWord(learner, course[1], "review", firstShown: Day.PlusDays(-5), dueOn: Day.PlusDays(2));
        AddLearnerWord(learner, course[2], "recalled", firstShown: Day.PlusDays(-6), dueOn: Day.PlusDays(3));
        await Db.SaveChangesAsync();

        await using var app = await StartAsync();
        using var response = await app.Client.GetAsync(SummaryPath);
        await AssertResponseAsync(response, "getLearnedWordsSummary", HttpStatusCode.OK);
        var summary = await BodyAsync(response);
        Assert.Equal(3, summary["seen_count"]!.GetValue<int>());
        Assert.Equal(1, summary["recalled_count"]!.GetValue<int>());
        Assert.Equal(2, summary["review_count"]!.GetValue<int>());
        // Слабке = review з забуваннями: лише course[0].
        Assert.Equal(1, summary["weak_count"]!.GetValue<int>());
    }

    [DockerFact]
    public async Task ReadsAreAuthorizedNotOnboardedReturns404AndLearnersAreIsolated()
    {
        var learner = await SeedLearnerAsync();
        var course = await SeedCourseAsync(2);
        AddLearnerWord(learner, course[0], "review", firstShown: Day.PlusDays(-5), dueOn: Day.PlusDays(1));
        await Db.SaveChangesAsync();

        await using var app = await StartAsync();

        app.Client.DefaultRequestHeaders.Authorization = null;
        using var anonymous = await app.Client.GetAsync(WordsPath);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        // Інший Telegram-користувач без онбордингу: список приховано як 404 learner.not_onboarded.
        Authenticate(app, UserId + 1);
        using var missing = await app.Client.GetAsync(WordsPath);
        await AssertResponseAsync(missing, "listLearnedWords", HttpStatusCode.NotFound);
        Assert.Equal("learner.not_onboarded", (await BodyAsync(missing))["code"]!.GetValue<string>());

        // Другий learner без слів бачить порожній список, а не слова першого.
        await SeedLearnerAsync(userId: UserId + 1);
        using var other = await app.Client.GetAsync(WordsPath);
        await AssertResponseAsync(other, "listLearnedWords", HttpStatusCode.OK);
        Assert.Empty((await BodyAsync(other))["items"]!.AsArray());
    }

    [DockerFact]
    public async Task InvalidQueryParametersReturn400()
    {
        await SeedLearnerAsync();
        await using var app = await StartAsync();
        using var response = await app.Client.GetAsync($"{WordsPath}?tab=bogus");
        await AssertResponseAsync(response, "listLearnedWords", HttpStatusCode.BadRequest);
        Assert.Equal("stats.invalid_request", (await BodyAsync(response))["code"]!.GetValue<string>());
    }

    private static void SetLemma(DictionaryEntry entry, string lemma) => entry.PublishedVersion!.LemmaPl = lemma;

    private AnswerEvent AddAnswer(Learner learner, DictionaryEntry entry, string outcome, string kind = "review")
    {
        var answer = new AnswerEvent
        {
            LearnerId = learner.Id,
            EntryId = entry.Id,
            VersionId = entry.PublishedVersionId!.Value,
            ExerciseKind = kind,
            Outcome = outcome,
            LearnerDay = Day,
            AnsweredAt = Clock.GetCurrentInstant(),
            TimeZone = "Europe/Warsaw",
            SchedulerVersion = "memorize-v1",
            StateBefore = "{}"
        };
        Db.AnswerEvents.Add(answer);
        return answer;
    }

    private void SetTranslation(DictionaryEntry entry, string textUk)
    {
        var translation = Db.VersionTranslations.Local
            .First(value => value.VersionId == entry.PublishedVersionId!.Value && value.Kind == "canonical");
        translation.TextUk = textUk;
    }

    private void AddTranslation(DictionaryEntry entry, string textUk)
    {
        var position = (short)Db.VersionTranslations.Local.Count(value => value.VersionId == entry.PublishedVersionId!.Value);
        Db.VersionTranslations.Add(new() { VersionId = entry.PublishedVersionId!.Value, Kind = "alternate", TextUk = textUk, Position = position });
    }

    private void AddLearnerWord(Learner learner, DictionaryEntry entry, string status,
        LocalDate firstShown, LocalDate dueOn, int interval = 2, int lapses = 0)
    {
        Db.LearnerWords.Add(new LearnerWord
        {
            LearnerId = learner.Id,
            EntryId = entry.Id,
            Status = status,
            FirstShownDay = firstShown,
            LastReviewDay = firstShown,
            DueOn = dueOn,
            IntervalDays = interval,
            Lapses = lapses,
            ModelState = "{}",
            SchedulerVersion = "memorize-v1"
        });
    }

    private async Task<Learner> SeedLearnerAsync(short limit = 3, long userId = UserId)
    {
        var learner = new Learner
        {
            TelegramUserId = userId,
            TimeZone = "Europe/Warsaw",
            TimeZoneConfirmed = true,
            DailyNewWordsLimit = limit,
            OnboardedAt = Clock.GetCurrentInstant(),
            CreatedAt = Clock.GetCurrentInstant()
        };
        Db.Learners.Add(learner);
        await Db.SaveChangesAsync();
        return learner;
    }

    private async Task<DictionaryEntry[]> SeedCourseAsync(int count)
    {
        var entries = new List<DictionaryEntry>();
        for (var index = 1; index <= count; index++)
        {
            var entry = new DictionaryEntry { ExternalId = Guid.NewGuid().ToString("N"), Domain = "everyday", CoursePosition = index };
            var version = ReportsPublicationTestData.NewVersion(entry);
            version.LemmaPl = $"slowo{index}";
            version.FrequencyRank = index;
            Db.EntryVersions.Add(version);
            Db.VersionTranslations.Add(new() { VersionId = version.Id, Kind = "canonical", TextUk = "жаба", Position = 0 });
            Db.VersionAcceptedAnswers.Add(new() { VersionId = version.Id, TextPl = version.LemmaPl, IsCanonical = true });
            await Db.SaveChangesAsync();
            entry.PublishedVersionId = version.Id;
            entry.PublishedVersion = version;
            entries.Add(entry);
        }
        await Db.SaveChangesAsync();
        return entries.ToArray();
    }

    private async Task<Guid[]> EntryIdsAsync(StartSessionTestApplication app, string path)
    {
        using var response = await app.Client.GetAsync(path);
        await AssertResponseAsync(response, "listLearnedWords", HttpStatusCode.OK);
        return (await BodyAsync(response))["items"]!.AsArray()
            .Select(node => node!["entry_id"]!.GetValue<Guid>()).ToArray();
    }

    private async Task<StartSessionTestApplication> StartAsync()
    {
        var app = await StartSessionTestApplication.StartAsync(_postgres.ConnectionString, Clock);
        Authenticate(app, UserId);
        return app;
    }

    private void Authenticate(StartSessionTestApplication app, long userId) =>
        app.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("tma",
            new TelegramInitDataSigner().Sign(userId, Clock.GetCurrentInstant()));

    private static async Task<JsonNode> BodyAsync(HttpResponseMessage response) =>
        JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

    private static async Task AssertResponseAsync(HttpResponseMessage response, string operationId, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        await ContractAssert.ResponseAsync(operationId, response);
    }
}
