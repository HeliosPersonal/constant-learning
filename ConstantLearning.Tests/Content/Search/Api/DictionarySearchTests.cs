using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using ConstantLearning.Features.Content.Data;
using ConstantLearning.Features.Content.Search.Forms;
using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Features.Learning.Data;
using ConstantLearning.Tests.Content.Data;
using ConstantLearning.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ConstantLearning.Tests.Content.Search.Api;

public sealed class DictionarySearchTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;
    private const string SearchPath = "/api/v1/dictionary/search";
    private const string EntryPath = "/api/v1/dictionary/entries/";
    private const long UserId = 900000015;

    [DockerFact]
    public async Task Ac30_FindsFoldedFormsBothLanguages_ExactFirst_OneCharacterExactOnly_AndAmbiguousLemmasDistinct()
    {
        await SeedLearnerAsync();
        var first = await SeedEntryAsync("zabawka", 1, "іграшка", ["żabami"]);
        var second = await SeedEntryAsync("żaba", 50, "жаба", ["żabami", "żabą", "żabie"], ["жабкою", "ґедзь"]);
        var single = await SeedEntryAsync("a", 100, "а", ["a"]);
        await using var app = await StartAsync();

        var exact = await SearchAsync(app, " ZABA ");
        Assert.Equal(new[] { second.Entry.Id, first.Entry.Id }, Ids(exact));
        Assert.Equal("żaba", exact["items"]![0]!["lemma_pl"]!.GetValue<string>());
        Assert.Equal("noun", exact["items"]![0]!["part_of_speech"]!.GetValue<string>());
        Assert.Equal("жаба", exact["items"]![0]!["short_meaning_uk"]!.GetValue<string>());
        Assert.Equal(new[] { first.Entry.Id, second.Entry.Id }, Ids(await SearchAsync(app, "ŻABAMI")));
        Assert.Equal(new[] { second.Entry.Id }, Ids(await SearchAsync(app, "жабк")));
        Assert.Equal(new[] { second.Entry.Id }, Ids(await SearchAsync(app, "ГЕДЗЬ")));
        Assert.Equal(new[] { single.Entry.Id }, Ids(await SearchAsync(app, "a")));
        Assert.Empty(Ids(await SearchAsync(app, "z")));
        Assert.Equal(2, Ids(await SearchAsync(app, "za")).Length);
        Assert.Equal(new[] { second.Entry.Id }, Ids(await SearchAsync(app, "żabie")));
        Assert.Equal(new[] { second.Entry.Id }, Ids(await SearchAsync(app, "z\u0307abie")));
    }

    [DockerFact]
    public async Task CursorPagination_RoundTripsExactRankCourseTiesAndNullPosition_WithoutDuplicates()
    {
        await SeedLearnerAsync();
        var entries = new[]
        {
            await SeedEntryAsync("prefix-a", null, "а", ["prefix-a"]),
            await SeedEntryAsync("prefix-b", 2, "б", ["prefix-b"]),
            await SeedEntryAsync("prefix-c", 2, "в", ["prefix-c"]),
            await SeedEntryAsync("prefix", 99, "г", ["prefix"]),
            await SeedEntryAsync("prefix-d", 1, "ґ", ["prefix-d"])
        };
        await using var app = await StartAsync();
        var all = Ids(await SearchAsync(app, "prefix"));
        Assert.Equal(entries[3].Entry.Id, all[0]);
        Assert.Equal(entries[4].Entry.Id, all[1]);
        Assert.Equal(entries[0].Entry.Id, all[^1]);
        var page = await SearchAsync(app, "prefix", "&limit=2");
        Assert.False(page["has_prev"]!.GetValue<bool>());
        Assert.True(page["has_next"]!.GetValue<bool>());
        var second = await SearchAsync(app, "prefix", "&limit=2&after=" + page["next_cursor"]!.GetValue<string>());
        Assert.True(second["has_prev"]!.GetValue<bool>());
        var third = await SearchAsync(app, "prefix", "&limit=2&after=" + second["next_cursor"]!.GetValue<string>());
        Assert.False(third["has_next"]!.GetValue<bool>());
        Assert.Null(third["next_cursor"]);
        Assert.Equal(all, Ids(page).Concat(Ids(second)).Concat(Ids(third)));
        var back = await SearchAsync(app, "prefix", "&limit=2&before=" + Ids(second)[0]);
        Assert.Equal(Ids(page), Ids(back));
        Assert.False(back["has_prev"]!.GetValue<bool>());
        Assert.True(back["has_next"]!.GetValue<bool>());
    }

    [DockerFact]
    public async Task Ac29_EntryCardAndSearchExposeOnlyOwnStatus_AndReadingDoesNotWriteLearningOrContent()
    {
        var own = await SeedLearnerAsync();
        var other = await SeedLearnerAsync(UserId + 1);
        var entry = await SeedEntryAsync("urząd", 1, "державна установа", ["urzędzie"], ["ужонд"]);
        Db.LearnerWords.AddRange(NewWord(own.Id, entry.Entry.Id, "review"), NewWord(other.Id, entry.Entry.Id, "recalled"));
        await Db.SaveChangesAsync();
        var before = await Db.LearnerWords.AsNoTracking().ToArrayAsync();
        await using var app = await StartAsync();
        var search = await SearchAsync(app, "urzedzie");
        Assert.Equal("review", search["items"]![0]!["word_status"]!.GetValue<string>());
        using var response = await app.Client.GetAsync(EntryPath + entry.Entry.Id);
        await AssertResponseAsync(response, "getDictionaryEntry", HttpStatusCode.OK);
        var card = await BodyAsync(response);
        Assert.Equal("review", card["word_status"]!.GetValue<string>());
        Assert.Equal("urząd", card["lemma_pl"]!.GetValue<string>());
        Assert.Null(card["gender"]);
        Assert.Equal("державна установа", card["translations_uk"]![0]!["text_uk"]!.GetValue<string>());
        Assert.Equal("local", card["translations_uk"]![1]!["kind"]!.GetValue<string>());
        Authenticate(app, other.TelegramUserId);
        Assert.Equal("recalled", (await SearchAsync(app, "urzad"))["items"]![0]!["word_status"]!.GetValue<string>());
        await SeedLearnerAsync(UserId + 2);
        Authenticate(app, UserId + 2);
        Assert.Equal("not_started", (await SearchAsync(app, "urzad"))["items"]![0]!["word_status"]!.GetValue<string>());
        var after = await Db.LearnerWords.AsNoTracking().ToArrayAsync();
        Assert.Equal(before.Select(word => (word.Id, word.Status, word.Version, word.DueOn)),
            after.Select(word => (word.Id, word.Status, word.Version, word.DueOn)));
        Assert.Empty(await Db.LearningSessions.ToArrayAsync());
        Assert.Empty(await Db.AnswerEvents.ToArrayAsync());
        Assert.Equal(1, (await Db.DictionaryEntries.AsNoTracking().SingleAsync()).CoursePosition);
    }

    [DockerFact]
    public async Task Ac31To33_OnlyCurrentPublishedApprovedVersionVisible_WithdrawalImmediate_StatusAndHistoryPreserved()
    {
        var learner = await SeedLearnerAsync();
        var approved = await SeedEntryAsync("żaba", 1, "жаба", ["żabie"]);
        var draft = await SeedEntryAsync("żabka", 2, "жабка", ["żabkami"], status: "draft");
        var review = await SeedEntryAsync("żabusia", 3, "жабуся", ["żabusiami"], status: "needs_review");
        var unpublished = await SeedEntryAsync("żabuniek", 4, "жабка", ["żabunkami"]);
        unpublished.Entry.PublishedVersionId = null;
        var stale = ReportsPublicationTestData.NewVersion(approved.Entry);
        stale.LemmaPl = "старе";
        Db.EntryVersions.Add(stale);
        Db.SearchForms.Add(new SearchForm { EntryId = approved.Entry.Id, VersionId = stale.Id, Lang = "pl",
            FormDisplay = "obsolete", FormFolded = "obsolete", CreatedAt = Clock.GetCurrentInstant() });
        Db.LearnerWords.Add(NewWord(learner.Id, approved.Entry.Id, "recalled"));
        await Db.SaveChangesAsync();
        await using var app = await StartAsync();
        Assert.Equal(new[] { approved.Entry.Id }, Ids(await SearchAsync(app, "zab")));
        Assert.Empty(Ids(await SearchAsync(app, "obsolete")));
        foreach (var hidden in new[] { draft.Entry.Id, review.Entry.Id, unpublished.Entry.Id, Guid.NewGuid() })
        {
            using var response = await app.Client.GetAsync(EntryPath + hidden);
            await AssertResponseAsync(response, "getDictionaryEntry", HttpStatusCode.NotFound, "dictionary.entry_not_found");
        }
        approved.Version.ValidationStatus = "needs_review";
        await Db.SaveChangesAsync();
        Assert.Empty(Ids(await SearchAsync(app, "zab")));
        using var withdrawn = await app.Client.GetAsync(EntryPath + approved.Entry.Id);
        await AssertResponseAsync(withdrawn, "getDictionaryEntry", HttpStatusCode.NotFound);
        Assert.Equal("recalled", (await Db.LearnerWords.AsNoTracking().SingleAsync()).Status);
        approved.Version.ValidationStatus = "approved";
        await Db.SaveChangesAsync();
        Assert.Equal("recalled", (await SearchAsync(app, "zab"))["items"]![0]!["word_status"]!.GetValue<string>());
        var empty = await SearchAsync(app, "бракуєслова");
        Assert.Empty(Ids(empty));
        Assert.False(empty["has_next"]!.GetValue<bool>());
        Assert.False(empty["has_prev"]!.GetValue<bool>());
        Assert.Null(empty["next_cursor"]);
    }

    [DockerFact]
    public async Task InvalidQueriesAndAuthentication_UseContractErrorEnvelopes_AndWildcardsAreLiteral()
    {
        await SeedLearnerAsync();
        await SeedEntryAsync("żaba", 1, "жаба", ["żabie"]);
        await using var app = await StartAsync();
        foreach (var suffix in new[] { "", "?q=", "?q=%20%20", "?q=%CC%81" })
        {
            using var response = await app.Client.GetAsync(SearchPath + suffix);
            await AssertResponseAsync(response, "searchDictionary", HttpStatusCode.BadRequest, "dictionary.query_empty");
        }
        foreach (var suffix in new[] { "?q=" + new string('a', 201), "?q=za&q=ba", "?q=za&limit=0",
                     "?q=za&limit=51", "?q=za&limit=abc", "?q=za&after=abc", "?q=za&after=",
                     "?q=za&before=abc", "?q=za&x=1", "?q=za&after=" + Guid.NewGuid(),
                     "?q=za&after=" + Guid.NewGuid() + "&before=" + Guid.NewGuid() })
        {
            using var response = await app.Client.GetAsync(SearchPath + suffix);
            await AssertResponseAsync(response, "searchDictionary", HttpStatusCode.BadRequest, "dictionary.invalid_request");
        }
        Assert.Empty(Ids(await SearchAsync(app, "%_")));
        Assert.Empty(Ids(await SearchAsync(app, "' OR 1=1--")));
        using var malformed = await app.Client.GetAsync(EntryPath + "broken");
        await AssertResponseAsync(malformed, "getDictionaryEntry", HttpStatusCode.NotFound);
        app.Client.DefaultRequestHeaders.Authorization = null;
        using var anonymous = await app.Client.GetAsync(SearchPath + "?q=za");
        await AssertResponseAsync(anonymous, "searchDictionary", HttpStatusCode.Unauthorized);
        using var anonymousCard = await app.Client.GetAsync(EntryPath + Guid.NewGuid());
        await AssertResponseAsync(anonymousCard, "getDictionaryEntry", HttpStatusCode.Unauthorized);
        app.Client.DefaultRequestHeaders.Authorization = new("tma", "hash=invalid");
        using var invalid = await app.Client.GetAsync(SearchPath + "?q=za");
        await AssertResponseAsync(invalid, "searchDictionary", HttpStatusCode.Unauthorized);
    }

    [DockerFact]
    public async Task CancelledHttpRequest_PropagatesRequestAbortedToEfSearchCommand()
    {
        await SeedLearnerAsync();
        await SeedEntryAsync("żaba", 1, "жаба", ["żabie"]);
        var interceptor = new BlockingSearchInterceptor();
        await using var app = await DictionaryTestApplication.StartAsync(_postgres.ConnectionString, Clock, interceptor);
        Authenticate(app, UserId);
        using var cancellation = new CancellationTokenSource();
        var pending = app.Client.GetAsync(SearchPath + "?q=zab", cancellation.Token);
        await interceptor.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.True(await interceptor.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    private async Task<DictionaryTestApplication> StartAsync()
    {
        var app = await DictionaryTestApplication.StartAsync(_postgres.ConnectionString, Clock);
        Authenticate(app, UserId);
        return app;
    }

    private void Authenticate(DictionaryTestApplication app, long userId) =>
        app.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("tma",
            new TelegramInitDataSigner().Sign(userId, Clock.GetCurrentInstant()));

    private async Task<Learner> SeedLearnerAsync(long userId = UserId)
    {
        var learner = new Learner { TelegramUserId = userId, FirstName = "Пошук", TimeZone = "Europe/Warsaw",
            TimeZoneConfirmed = true, DailyNewWordsLimit = 5, CreatedAt = Clock.GetCurrentInstant() };
        Db.Learners.Add(learner);
        await Db.SaveChangesAsync();
        return learner;
    }

    private async Task<(DictionaryEntry Entry, EntryVersion Version)> SeedEntryAsync(string lemma, int? position,
        string translation, string[] forms, string[]? ukrainian = null, string status = "approved")
    {
        var entry = new DictionaryEntry { ExternalId = Guid.NewGuid().ToString("N"), Domain = "everyday", CoursePosition = position };
        var version = ReportsPublicationTestData.NewVersion(entry);
        version.LemmaPl = lemma;
        version.ValidationStatus = status;
        Db.EntryVersions.Add(version);
        Db.VersionTranslations.Add(new() { VersionId = version.Id, Kind = "canonical", TextUk = translation, Position = 0 });
        foreach (var (text, index) in (ukrainian ?? []).Select((text, index) => (text, index)))
            Db.VersionTranslations.Add(new() { VersionId = version.Id, Kind = "local", TextUk = text, Position = checked((short)(index + 1)) });
        await Db.SaveChangesAsync();
        entry.PublishedVersionId = version.Id;
        foreach (var (lang, values) in new[] { ("pl", forms.Append(lemma)), ("uk", (ukrainian ?? []).Append(translation)) })
            foreach (var value in values.Distinct()) Db.SearchForms.Add(new SearchForm
            {
                EntryId = entry.Id, VersionId = version.Id, Lang = lang, FormDisplay = value,
                FormFolded = SearchTextNormalizer.Fold(value), IsLemma = value == lemma, CreatedAt = Clock.GetCurrentInstant()
            });
        await Db.SaveChangesAsync();
        return (entry, version);
    }

    private static LearnerWord NewWord(Guid learnerId, Guid entryId, string status) => new()
    {
        LearnerId = learnerId, EntryId = entryId, Status = status, ModelState = "{}", SchedulerVersion = "memorize-v1"
    };

    private static async Task<JsonNode> SearchAsync(DictionaryTestApplication app, string q, string suffix = "")
    {
        using var response = await app.Client.GetAsync(SearchPath + "?q=" + Uri.EscapeDataString(q) + suffix);
        await AssertResponseAsync(response, "searchDictionary", HttpStatusCode.OK);
        return await BodyAsync(response);
    }

    private static Guid[] Ids(JsonNode page) => page["items"]!.AsArray().Select(item => item!["entry_id"]!.GetValue<Guid>()).ToArray();
    private static async Task<JsonNode> BodyAsync(HttpResponseMessage response) => JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

    private static async Task AssertResponseAsync(HttpResponseMessage response, string operation, HttpStatusCode status, string? code = null)
    {
        Assert.Equal(status, response.StatusCode);
        await ContractAssert.ResponseAsync(operation, response);
        if (code is not null) Assert.Equal(code, (await BodyAsync(response))["code"]!.GetValue<string>());
    }

    private sealed class BlockingSearchInterceptor : DbCommandInterceptor
    {
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("search_forms", StringComparison.Ordinal))
            {
                Started.TrySetResult(true);
                try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
                catch (OperationCanceledException) { Cancelled.TrySetResult(cancellationToken.IsCancellationRequested); throw; }
            }
            return result;
        }
    }
}
