using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using ConstantLearning.Features.Content.Data;
using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Features.Identity.ProgressReset;
using ConstantLearning.Features.Learning.Data;
using ConstantLearning.Tests.Content.Data;
using ConstantLearning.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;

namespace ConstantLearning.Tests.Identity.ProgressReset;

/// <summary>
/// End-to-end скидання на справжній базі зі справжніми обробниками Content і Learning:
/// повнота стирання (AC-07/AC-08), збереження reports без привʼязки (AC-10), незмінність налаштувань
/// (AC-06), повний відкат при збої (AC-04) і серіалізація одночасних скидань (AC-11).
/// </summary>
public sealed class ProgressResetIntegrationTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;
    private const string Path = "/api/v1/learners/me/progress/reset";
    private const long TelegramUserId = 970000002;

    [DockerFact]
    public async Task Reset_WithRealHandlers_ErasesProgressGraph_KeepsDetachedReports_PreservesSettings()
    {
        await SeedProgressGraphAsync();
        await using var app = await ResetTestApplication.StartAsync(_postgres.ConnectionString, Clock);

        using var response = await PostAsync(app);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractAssert.ResponseAsync("resetLearnerProgress", response);
        Assert.True((await BodyAsync(response))["erased"]!.GetValue<bool>());

        // AC-07/AC-08: прогрес стерто повністю.
        Assert.Equal(0, await Db.LearnerWords.CountAsync());
        Assert.Equal(0, await Db.LearningSessions.CountAsync());
        Assert.Equal(0, await Db.SessionItems.CountAsync());
        Assert.Equal(0, await Db.AnswerEvents.CountAsync());
        Assert.Equal(0, await Db.AnswerNeutralizations.CountAsync());

        // AC-10: report лишається, лише відвʼязаний від стертої відповіді.
        var report = await Db.ContentReports.AsNoTracking().SingleAsync();
        Assert.Null(report.AnswerEventId);
        Assert.Equal("answer_dispute", report.Origin);
        Assert.Equal("pending_verification", report.Status);

        // AC-06: профіль і налаштування недоторкані, лише маркери прогресу обнулено.
        var learner = await Db.Learners.AsNoTracking().SingleAsync();
        Assert.Null(learner.CourseCompletedAt);
        Assert.Null(learner.NewWordsAvailableFrom);
        Assert.Equal((short)5, learner.DailyNewWordsLimit);
        Assert.Equal("Europe/Warsaw", learner.TimeZone);
        Assert.NotNull(learner.OnboardedAt);
        Assert.Single(await Db.ReminderSchedules.AsNoTracking().ToListAsync());
    }

    [DockerFact]
    public async Task Reset_WhenHandlerFailsMidErase_RollsBackEverything_AndReportsFailed()
    {
        await SeedProgressGraphAsync();
        await using var app = await ResetTestApplication.StartAsync(_postgres.ConnectionString, Clock,
            services => services.AddScoped<IProgressResetHandler, FaultyEraseHandler>());

        using var response = await PostAsync(app);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await ContractAssert.ResponseAsync("resetLearnerProgress", response);
        Assert.Equal("progress_reset.failed", (await BodyAsync(response))["code"]!.GetValue<string>());

        // Повний відкат: справжні обробники встигли видалити, але транзакція повернула все.
        Assert.Equal(1, await Db.LearnerWords.CountAsync());
        Assert.Equal(1, await Db.LearningSessions.CountAsync());
        Assert.Equal(1, await Db.SessionItems.CountAsync());
        Assert.Equal(1, await Db.AnswerEvents.CountAsync());
        Assert.Equal(1, await Db.AnswerNeutralizations.CountAsync());
        var report = await Db.ContentReports.AsNoTracking().SingleAsync();
        Assert.NotNull(report.AnswerEventId);
        var learner = await Db.Learners.AsNoTracking().SingleAsync();
        Assert.NotNull(learner.CourseCompletedAt);
        Assert.NotNull(learner.NewWordsAvailableFrom);
    }

    [DockerFact]
    public async Task ConcurrentResets_SerializeOnLearnerRowLock_ExactlyOneErases()
    {
        await SeedProgressGraphAsync();
        await using var app = await ResetTestApplication.StartAsync(_postgres.ConnectionString, Clock);

        var task1 = PostAsync(app);
        var task2 = PostAsync(app);
        using var concurrentA = await task1;
        using var concurrentB = await task2;

        Assert.Equal(HttpStatusCode.OK, concurrentA.StatusCode);
        Assert.Equal(HttpStatusCode.OK, concurrentB.StatusCode);
        var erased = new[]
        {
            (await BodyAsync(concurrentA))["erased"]!.GetValue<bool>(),
            (await BodyAsync(concurrentB))["erased"]!.GetValue<bool>()
        };
        // Рядок learner під FOR UPDATE: скидання йдуть по черзі, стерти встигає лише одне.
        Assert.Single(erased, value => value);
        Assert.Single(erased, value => !value);
        Assert.Equal(0, await Db.LearnerWords.CountAsync());
    }

    private async Task SeedProgressGraphAsync()
    {
        var now = Clock.GetCurrentInstant();
        var day = new LocalDate(2026, 10, 31);

        var entry = new DictionaryEntry { ExternalId = $"reset-{Guid.NewGuid():N}", Domain = "everyday", CoursePosition = 1 };
        var version = ReportsPublicationTestData.NewVersion(entry);
        Db.EntryVersions.Add(version);

        var learner = new Learner
        {
            TelegramUserId = TelegramUserId,
            FirstName = "Test User",
            TimeZone = "Europe/Warsaw",
            TimeZoneConfirmed = true,
            DailyNewWordsLimit = 5,
            CanMessage = true,
            OnboardedAt = now,
            CreatedAt = now,
            CourseCompletedAt = now,
            NewWordsAvailableFrom = Instant.FromUtc(2026, 10, 31, 22, 0),
            ReminderSchedules = [new ReminderSchedule { TargetLanguage = "pl", LocalTime = new LocalTime(19, 0), DaysOfWeek = 1, CreatedAt = now }]
        };
        Db.Learners.Add(learner);

        Db.LearnerWords.Add(new LearnerWord
        {
            LearnerId = learner.Id,
            EntryId = entry.Id,
            Status = "review",
            FirstShownDay = day.PlusDays(-10),
            DueOn = day,
            IntervalDays = 2,
            ModelState = "{\"forgetting_rate\":0.3}",
            SchedulerVersion = "memorize-v1"
        });
        await Db.SaveChangesAsync();
        entry.PublishedVersionId = version.Id;

        var session = new LearningSession
        {
            LearnerId = learner.Id,
            LearnerDay = day,
            EndsAt = Instant.FromUtc(2026, 10, 31, 23, 0),
            Status = "active",
            Kind = "plan",
            LastActivityAt = now,
            CreatedAt = now
        };
        var item = new SessionItem
        {
            SessionId = session.Id,
            EntryId = entry.Id,
            Kind = "review",
            Position = 1,
            State = "pending",
            CreatedAt = now
        };
        Db.LearningSessions.Add(session);
        Db.SessionItems.Add(item);
        await Db.SaveChangesAsync();

        var answer = new AnswerEvent
        {
            LearnerId = learner.Id,
            EntryId = entry.Id,
            VersionId = version.Id,
            SessionItemId = item.Id,
            ExerciseKind = "review",
            Outcome = "mistake_typo",
            RawAnswer = "zaba",
            NormalizedAnswer = "zaba",
            AnsweredAt = now,
            LearnerDay = day,
            TimeZone = "Europe/Warsaw",
            SchedulerVersion = "memorize-v1",
            StateBefore = """{"rate":0.125,"history":[true,null,2]}"""
        };
        Db.AnswerEvents.Add(answer);
        await Db.SaveChangesAsync();

        var report = new ContentReport
        {
            LearnerId = learner.Id,
            EntryId = entry.Id,
            VersionId = version.Id,
            AnswerEventId = answer.Id,
            Origin = "answer_dispute",
            Status = "pending_verification",
            CreatedAt = now
        };
        Db.ContentReports.Add(report);
        await Db.SaveChangesAsync();

        Db.AnswerNeutralizations.Add(new AnswerNeutralization { AnswerEventId = answer.Id, ContentReportId = report.Id });
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
    }

    private static async Task<JsonNode> BodyAsync(HttpResponseMessage response) =>
        JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

    private async Task<HttpResponseMessage> PostAsync(ResetTestApplication app)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Path);
        request.Headers.Authorization = new AuthenticationHeaderValue("tma",
            new TelegramInitDataSigner().Sign(TelegramUserId, Clock.GetCurrentInstant()));
        return await app.Client.SendAsync(request);
    }

    private sealed class FaultyEraseHandler : IProgressResetHandler
    {
        public ProgressResetPhase Phase => ProgressResetPhase.EraseProgress;

        public Task<bool> HasPendingDataAsync(Guid learnerId, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task EraseAsync(ProgressResetRequested request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Імітація збою посеред скидання.");
    }
}
