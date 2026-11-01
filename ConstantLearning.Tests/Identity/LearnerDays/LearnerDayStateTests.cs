using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Features.Identity.LearnerDays;
using ConstantLearning.Platform.Persistence;
using ConstantLearning.Tests.Identity.Settings;
using ConstantLearning.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;

namespace ConstantLearning.Tests.Identity.LearnerDays;

public sealed class LearnerDayStateTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;

    [DockerFact]
    public async Task SharedPort_RequiresCallerTransaction_AndInitializesPersistedDayWithoutChangingIssuanceBoundary()
    {
        var learner = await SeedAsync();
        await using var app = await SettingsTestApplication.StartAsync(_postgres.ConnectionString, Clock);
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var days = scope.ServiceProvider.GetRequiredService<ILearnerDayStateService>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => days.GetForUpdateAsync(learner.Id));
        await using var transaction = await db.Database.BeginTransactionAsync();
        var state = await days.GetForUpdateAsync(learner.Id);
        Assert.NotNull(state);
        Assert.Equal(new LocalDate(2026, 1, 15), state.Day.Date);
        Assert.Equal("Europe/Warsaw", state.Day.TimeZoneAtStart);
        Assert.Equal(Instant.FromUtc(2026, 1, 15, 23, 0), state.Day.EndsAt);
        await transaction.CommitAsync();
        var stored = await Db.Learners.AsNoTracking().SingleAsync();
        Assert.Equal(state.Day.Date, stored.CurrentDayDate);
        Assert.Equal(state.Day.StartsAt, stored.CurrentDayStartsAt);
        Assert.Equal(state.Day.EndsAt, stored.CurrentDayEndsAt);
        Assert.Equal(state.Day.OriginalEndsAt, stored.CurrentDayOriginalEndsAt);
        Assert.Null(stored.NewWordsAvailableFrom);
    }

    [DockerFact]
    public async Task SharedPort_AppliesMatureLimitAndRollsForwardAfterInactiveDays_InFreshProcessScope()
    {
        var learner = await SeedAsync();
        learner.PendingDailyNewWordsLimit = 10;
        learner.PendingLimitEffectiveFrom = Instant.FromUtc(2026, 1, 15, 23, 0);
        await Db.SaveChangesAsync();
        Clock.Advance(Duration.FromDays(5));
        await using var app = await SettingsTestApplication.StartAsync(_postgres.ConnectionString, Clock);
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var state = await scope.ServiceProvider.GetRequiredService<ILearnerDayStateService>().GetForUpdateAsync(learner.Id);
        Assert.NotNull(state);
        Assert.Equal(10, state.Learner.DailyNewWordsLimit);
        Assert.Null(state.Learner.PendingDailyNewWordsLimit);
        Assert.Null(state.Learner.PendingLimitEffectiveFrom);
        Assert.Equal(new LocalDate(2026, 1, 20), state.Day.Date);
        await transaction.CommitAsync();
        Assert.Equal(10, (await Db.Learners.AsNoTracking().SingleAsync()).DailyNewWordsLimit);
    }

    [DockerFact]
    public async Task SharedPort_RejectsPartialSnapshots_WithoutGuessingMissingFields()
    {
        var learner = await SeedAsync();
        await using var app = await SettingsTestApplication.StartAsync(_postgres.ConnectionString, Clock);
        Action<Learner>[] partialSnapshots =
        [
            value => value.CurrentDayDate = new LocalDate(2026, 1, 15),
            value => value.CurrentDayStartsAt = Instant.FromUtc(2026, 1, 14, 23, 0),
            value => value.CurrentDayEndsAt = Instant.FromUtc(2026, 1, 15, 23, 0),
            value => value.CurrentDayOriginalEndsAt = Instant.FromUtc(2026, 1, 15, 23, 0),
            value => value.CurrentDayTimeZoneAtStart = "Europe/Warsaw"
        ];
        foreach (var configure in partialSnapshots)
        {
            learner.CurrentDayDate = null;
            learner.CurrentDayStartsAt = null;
            learner.CurrentDayEndsAt = null;
            learner.CurrentDayOriginalEndsAt = null;
            learner.CurrentDayTimeZoneAtStart = null;
            configure(learner);
            await Db.SaveChangesAsync();
            await using var scope = app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync();
            var days = scope.ServiceProvider.GetRequiredService<ILearnerDayStateService>();
            await Assert.ThrowsAsync<InvalidOperationException>(() => days.GetForUpdateAsync(learner.Id));
        }
    }

    [DockerFact]
    public async Task SharedPort_RejectsInvalidOrderedBoundaries_AndMissingOrIncompleteLearners()
    {
        var learner = await SeedAsync();
        learner.CurrentDayDate = new LocalDate(2026, 1, 15);
        learner.CurrentDayTimeZoneAtStart = "Europe/Warsaw";
        learner.CurrentDayStartsAt = Instant.FromUtc(2026, 1, 15, 23, 0);
        learner.CurrentDayOriginalEndsAt = learner.CurrentDayStartsAt;
        learner.CurrentDayEndsAt = learner.CurrentDayStartsAt;
        await Db.SaveChangesAsync();
        await using var app = await SettingsTestApplication.StartAsync(_postgres.ConnectionString, Clock);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync();
            var days = scope.ServiceProvider.GetRequiredService<ILearnerDayStateService>();
            Assert.Null(await days.GetForUpdateAsync(Guid.NewGuid()));
            await Assert.ThrowsAsync<ArgumentException>(() => days.GetForUpdateAsync(learner.Id));
        }

        learner.OnboardedAt = null;
        await Db.SaveChangesAsync();
        await using var incompleteScope = app.Services.CreateAsyncScope();
        var incompleteDb = incompleteScope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var incompleteTransaction = await incompleteDb.Database.BeginTransactionAsync();
        Assert.Null(await incompleteScope.ServiceProvider.GetRequiredService<ILearnerDayStateService>().GetForUpdateAsync(learner.Id));
    }

    [DockerFact]
    public async Task SharedPort_RowLockSpansCallerOperation_AndNextConsumerSeesCommittedDay()
    {
        var learner = await SeedAsync();
        await using var app = await SettingsTestApplication.StartAsync(_postgres.ConnectionString, Clock);
        await using var firstScope = app.Services.CreateAsyncScope();
        await using var secondScope = app.Services.CreateAsyncScope();
        var firstDb = firstScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var secondDb = secondScope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var firstTransaction = await firstDb.Database.BeginTransactionAsync();
        await using var secondTransaction = await secondDb.Database.BeginTransactionAsync();
        var firstDays = firstScope.ServiceProvider.GetRequiredService<ILearnerDayStateService>();
        var first = await firstDays.GetForUpdateAsync(learner.Id);
        Assert.NotNull(first);
        var changed = firstDays.ChangeTimeZone(first, "America/New_York");
        await firstDb.SaveChangesAsync();
        var secondRead = secondScope.ServiceProvider.GetRequiredService<ILearnerDayStateService>().GetForUpdateAsync(learner.Id);
        Assert.NotSame(secondRead, await Task.WhenAny(secondRead, Task.Delay(150)));
        await firstTransaction.CommitAsync();
        var second = await secondRead.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.NotNull(second);
        Assert.Equal("America/New_York", second.Learner.TimeZone);
        Assert.Equal(changed.Day, second.Day);
        await secondTransaction.CommitAsync();
    }

    private async Task<Learner> SeedAsync()
    {
        Clock.Reset(Instant.FromUtc(2026, 1, 15, 11, 0));
        var learner = new Learner
        {
            TelegramUserId = 900000001, TimeZone = "Europe/Warsaw", TimeZoneConfirmed = true,
            DailyNewWordsLimit = 5, OnboardedAt = Clock.GetCurrentInstant(), CreatedAt = Clock.GetCurrentInstant()
        };
        Db.Learners.Add(learner);
        await Db.SaveChangesAsync();
        return learner;
    }
}
