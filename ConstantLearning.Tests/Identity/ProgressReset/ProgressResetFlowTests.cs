using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Features.Identity.ProgressReset;
using ConstantLearning.Platform.Persistence;
using ConstantLearning.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NodaTime;

namespace ConstantLearning.Tests.Identity.ProgressReset;

public sealed class ProgressResetFlowTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;
    private const string Path = "/api/v1/learners/me/progress/reset";
    private const long TelegramUserId = 900000001;

    [DockerFact]
    public async Task Ac01_ErasesViaHandlers_ResetsProgressMarkers_AndReportsErased()
    {
        var learner = await SeedAsync();
        var state = new ProbeState();
        var metrics = new CapturingMetrics();
        await using var app = await StartAsync(ConfigureProbe(state, metrics));

        using var response = await PostAsync(app);

        await AssertAsync(response, HttpStatusCode.OK);
        Assert.True((await BodyAsync(response))["erased"]!.GetValue<bool>());
        Assert.Equal(1, state.EraseCalls);
        var stored = await StoredAsync();
        Assert.Null(stored.CourseCompletedAt);
        Assert.Null(stored.NewWordsAvailableFrom);
        Assert.Equal(ProgressResetOutcomeKind.Erased, Assert.Single(metrics.Outcomes));
    }

    [DockerFact]
    public async Task Ac09_EmptyProgress_CommitsWithoutErasing_AndReportsNotErased()
    {
        await SeedAsync(withMarkers: false);
        var state = new ProbeState { PendingBefore = false };
        var metrics = new CapturingMetrics();
        await using var app = await StartAsync(ConfigureProbe(state, metrics));

        using var first = await PostAsync(app);
        await AssertAsync(first, HttpStatusCode.OK);
        Assert.False((await BodyAsync(first))["erased"]!.GetValue<bool>());

        using var second = await PostAsync(app);
        await AssertAsync(second, HttpStatusCode.OK);
        Assert.False((await BodyAsync(second))["erased"]!.GetValue<bool>());

        Assert.Equal(0, state.EraseCalls);
        Assert.Equal([ProgressResetOutcomeKind.Empty, ProgressResetOutcomeKind.Empty], metrics.Outcomes);
    }

    [DockerFact]
    public async Task Ac04_IntegrityCheckFails_RollsBackAndReportsFailedWithoutChangingProgress()
    {
        var learner = await SeedAsync();
        var state = new ProbeState { PendingAfter = true };
        var metrics = new CapturingMetrics();
        await using var app = await StartAsync(ConfigureProbe(state, metrics));

        using var response = await PostAsync(app);

        await AssertAsync(response, HttpStatusCode.InternalServerError, "progress_reset.failed");
        Assert.Equal(1, state.EraseCalls);
        var stored = await StoredAsync();
        Assert.Equal(learner.CourseCompletedAt, stored.CourseCompletedAt);
        Assert.Equal(learner.NewWordsAvailableFrom, stored.NewWordsAvailableFrom);
        Assert.Equal(ProgressResetOutcomeKind.IntegrityFailed, Assert.Single(metrics.Outcomes));
    }

    [DockerFact]
    public async Task Ac04_HandlerThrows_RollsBackAndReportsFailedWithoutChangingProgress()
    {
        var learner = await SeedAsync();
        var state = new ProbeState { Throw = true };
        var metrics = new CapturingMetrics();
        await using var app = await StartAsync(ConfigureProbe(state, metrics));

        using var response = await PostAsync(app);

        await AssertAsync(response, HttpStatusCode.InternalServerError, "progress_reset.failed");
        var stored = await StoredAsync();
        Assert.Equal(learner.CourseCompletedAt, stored.CourseCompletedAt);
        Assert.Equal(learner.NewWordsAvailableFrom, stored.NewWordsAvailableFrom);
        Assert.Equal(ProgressResetOutcomeKind.Failed, Assert.Single(metrics.Outcomes));
    }

    [DockerFact]
    public async Task Ac05_RejectsUnauthenticated_ForeignAndNotOnboarded_WithoutTouchingProgress()
    {
        var learner = await SeedAsync();
        await using var app = await StartAsync(ConfigureProbe(new ProbeState(), new CapturingMetrics()));

        using var anonymous = await app.Client.PostAsync(Path, content: null);
        await AssertAsync(anonymous, HttpStatusCode.Unauthorized, "auth.invalid_init_data");

        using var foreign = await PostAsync(app, TelegramUserId + 1, $"{Path}?learner_id={learner.Id}");
        await AssertAsync(foreign, HttpStatusCode.NotFound, "learner.not_onboarded");

        learner.OnboardedAt = null;
        await Db.SaveChangesAsync();
        using var incomplete = await PostAsync(app);
        await AssertAsync(incomplete, HttpStatusCode.NotFound, "learner.not_onboarded");

        var stored = await StoredAsync();
        Assert.NotNull(stored.CourseCompletedAt);
        Assert.NotNull(stored.NewWordsAvailableFrom);
    }

    [DockerFact]
    public async Task Ac06_PreservesProfileAndSettings_WhileClearingProgressMarkers()
    {
        var learner = await SeedAsync();
        learner.PendingDailyNewWordsLimit = 10;
        learner.PendingLimitEffectiveFrom = Instant.FromUtc(2026, 1, 16, 22, 0);
        learner.ReminderSchedules.Add(new ReminderSchedule { TargetLanguage = "pl", LocalTime = new LocalTime(19, 0), DaysOfWeek = 1 });
        await Db.SaveChangesAsync();
        await using var app = await StartAsync(ConfigureProbe(new ProbeState(), new CapturingMetrics()));

        using var response = await PostAsync(app);

        await AssertAsync(response, HttpStatusCode.OK);
        Assert.True((await BodyAsync(response))["erased"]!.GetValue<bool>());
        var stored = await StoredAsync();
        Assert.Equal("Test User", stored.FirstName);
        Assert.Equal("Europe/Warsaw", stored.TimeZone);
        Assert.Equal(TelegramUserId, stored.TelegramUserId);
        Assert.Equal((short)5, stored.DailyNewWordsLimit);
        Assert.Equal((short?)10, stored.PendingDailyNewWordsLimit);
        Assert.Equal(Instant.FromUtc(2026, 1, 16, 22, 0), stored.PendingLimitEffectiveFrom);
        Assert.NotNull(stored.OnboardedAt);
        Assert.Single(await Db.ReminderSchedules.AsNoTracking().ToListAsync());
        Assert.Null(stored.CourseCompletedAt);
        Assert.Null(stored.NewWordsAvailableFrom);
    }

    [DockerFact]
    public async Task HandlersRunInPhaseOrder_DetachBeforeErase()
    {
        await SeedAsync();
        var order = new ConcurrentQueue<ProgressResetPhase>();
        var detach = new ProbeState { Order = order };
        var erase = new ProbeState { Order = order };
        await using var app = await StartAsync(services =>
        {
            services.RemoveAll<IProgressResetHandler>();
            services.AddScoped<IProgressResetHandler>(sp =>
                new ProbeResetHandler(sp.GetRequiredService<AppDbContext>(), erase, ProgressResetPhase.EraseProgress));
            services.AddScoped<IProgressResetHandler>(sp =>
                new ProbeResetHandler(sp.GetRequiredService<AppDbContext>(), detach, ProgressResetPhase.DetachReferences));
            services.RemoveAll<IProgressResetMetrics>();
            services.AddSingleton<IProgressResetMetrics>(new CapturingMetrics());
        });

        using var response = await PostAsync(app);
        await AssertAsync(response, HttpStatusCode.OK);
        Assert.Equal([ProgressResetPhase.DetachReferences, ProgressResetPhase.EraseProgress], order);
    }

    private static Action<IServiceCollection> ConfigureProbe(ProbeState state, CapturingMetrics metrics) => services =>
    {
        services.RemoveAll<IProgressResetHandler>();
        services.AddScoped<IProgressResetHandler>(sp =>
            new ProbeResetHandler(sp.GetRequiredService<AppDbContext>(), state, ProgressResetPhase.EraseProgress));
        services.RemoveAll<IProgressResetMetrics>();
        services.AddSingleton<IProgressResetMetrics>(metrics);
    };

    private async Task<Learner> SeedAsync(bool withMarkers = true)
    {
        Clock.Reset(Instant.FromUtc(2026, 1, 15, 11, 0));
        var learner = new Learner
        {
            TelegramUserId = TelegramUserId, FirstName = "Test User", TimeZone = "Europe/Warsaw",
            TimeZoneConfirmed = true, DailyNewWordsLimit = 5, CanMessage = true,
            OnboardedAt = Clock.GetCurrentInstant(), CreatedAt = Clock.GetCurrentInstant()
        };
        if (withMarkers)
        {
            learner.CourseCompletedAt = Clock.GetCurrentInstant();
            learner.NewWordsAvailableFrom = Instant.FromUtc(2026, 1, 15, 22, 0);
        }

        Db.Learners.Add(learner);
        await Db.SaveChangesAsync();
        return learner;
    }

    private Task<Learner> StoredAsync() => Db.Learners.AsNoTracking().SingleAsync();

    private Task<ResetTestApplication> StartAsync(Action<IServiceCollection>? configure = null) =>
        ResetTestApplication.StartAsync(_postgres.ConnectionString, Clock, configure);

    private async Task<HttpResponseMessage> PostAsync(ResetTestApplication app, long telegramUserId = TelegramUserId, string path = Path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("tma",
            new TelegramInitDataSigner().Sign(telegramUserId, Clock.GetCurrentInstant()));
        return await app.Client.SendAsync(request);
    }

    private static async Task<JsonNode> BodyAsync(HttpResponseMessage response) =>
        JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

    private static async Task AssertAsync(HttpResponseMessage response, HttpStatusCode status, string? code = null)
    {
        Assert.Equal(status, response.StatusCode);
        await ContractAssert.ResponseAsync("resetLearnerProgress", response);
        if (code is not null)
        {
            Assert.Equal(code, (await BodyAsync(response))["code"]!.GetValue<string>());
        }
    }

    private sealed class ResetProbeException : Exception;

    private sealed class ProbeState
    {
        public bool PendingBefore { get; init; }
        public bool PendingAfter { get; init; }
        public bool Throw { get; init; }
        public int EraseCalls;
        public ConcurrentQueue<ProgressResetPhase>? Order { get; init; }
    }

    private sealed class ProbeResetHandler(AppDbContext db, ProbeState state, ProgressResetPhase phase) : IProgressResetHandler
    {
        public ProgressResetPhase Phase => phase;

        public Task<bool> HasPendingDataAsync(Guid learnerId, CancellationToken cancellationToken) =>
            Task.FromResult(state.EraseCalls > 0 ? state.PendingAfter : state.PendingBefore);

        public Task EraseAsync(ProgressResetRequested request, CancellationToken cancellationToken)
        {
            Assert.NotNull(db.Database.CurrentTransaction);
            Interlocked.Increment(ref state.EraseCalls);
            state.Order?.Enqueue(phase);
            return state.Throw ? throw new ResetProbeException() : Task.CompletedTask;
        }
    }

    private sealed class CapturingMetrics : IProgressResetMetrics
    {
        public ConcurrentQueue<ProgressResetOutcomeKind> Outcomes { get; } = new();

        public void RecordReset(ProgressResetOutcomeKind outcome, TimeSpan elapsed) => Outcomes.Enqueue(outcome);
    }
}
