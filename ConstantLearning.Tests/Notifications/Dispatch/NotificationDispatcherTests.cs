using ConstantLearning.Features.Content;
using ConstantLearning.Features.Content.Notifications;
using ConstantLearning.Features.Identity;
using ConstantLearning.Features.Notifications;
using ConstantLearning.Features.Notifications.Data;
using ConstantLearning.Features.Notifications.Dispatch;
using ConstantLearning.Platform;
using ConstantLearning.Platform.Persistence;
using ConstantLearning.Platform.Telegram;
using ConstantLearning.Tests.Content.Data;
using ConstantLearning.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace ConstantLearning.Tests.Notifications.Dispatch;

public sealed class NotificationDispatcherTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;

    [DockerFact]
    public async Task Tick_DrainsOneThousandDueRowsWithinBoundedBatch()
    {
        var now = Clock.GetCurrentInstant();
        Db.NotificationDispatches.AddRange(Enumerable.Range(0, 1000).Select(index => new NotificationDispatch
        {
            RecipientTelegramUserId = 500000 + index,
            Kind = "reminder",
            LocalDate = now.InUtc().Date,
            DueAt = now,
            Status = "pending"
        }));
        await Db.SaveChangesAsync();
        var telegram = new StubTelegram();
        await using var provider = CreateProvider(telegram);
        await TickAsync(provider);
        telegram.Calls.Should().Be(1000);
        (await Db.NotificationDispatches.CountAsync(dispatch => dispatch.Status == "sent")).Should().Be(1000);
    }

    [DockerFact]
    public async Task SummaryRejectedThenMessagingDisabled_ReleasesReservationWhenSkipped()
    {
        var root = await ReportsPublicationTestData.CreateRootAsync(Db, Clock);
        var dispatch = await CreateAsync("report_outcomes", root.Learner.TelegramUserId, root.Learner.Id);
        var report = ReportsPublicationTestData.NewReport(root);
        report.SummaryDispatchId = dispatch.Id;
        Db.ContentReports.Add(report);
        await Db.SaveChangesAsync();
        var telegram = new StubTelegram(() => throw new TelegramDeliveryException(TelegramDeliveryFailure.RejectedTransient));
        await using var provider = CreateProvider(telegram);
        await TickAsync(provider);
        (await ReadAsync(dispatch.Id)).Status.Should().Be("pending");
        (await Db.ContentReports.AsNoTracking().SingleAsync()).SummaryDispatchId.Should().Be(dispatch.Id);
        await Db.Learners.Where(learner => learner.Id == root.Learner.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(learner => learner.CanMessage, false));
        Clock.Advance(Duration.FromSeconds(30));
        await TickAsync(provider);
        (await ReadAsync(dispatch.Id)).Status.Should().Be("skipped");
        (await Db.ContentReports.AsNoTracking().SingleAsync()).SummaryDispatchId.Should().BeNull();
        telegram.Calls.Should().Be(1);
    }
    [DockerFact]
    public async Task ParallelTicks_CommitClaimBeforeSending_AndNeverSendTwice()
    {
        var dispatch = await CreateAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var telegram = new StubTelegram(async () =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(10));
        });
        await using var provider = CreateProvider(telegram);
        var first = TickAsync(provider);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var claimed = await ReadAsync(dispatch.Id);
            claimed.Status.Should().Be("sending");
            claimed.Attempts.Should().Be(1);
            claimed.ClaimedAt.Should().Be(Clock.GetCurrentInstant());
            await TickAsync(provider);
            telegram.Calls.Should().Be(1);
        }
        finally
        {
            release.TrySetResult();
            await first;
        }

        await TickAsync(provider);
        telegram.Calls.Should().Be(1);
        var sent = await ReadAsync(dispatch.Id);
        sent.Status.Should().Be("sent");
        sent.SentAt.Should().Be(Clock.GetCurrentInstant());
    }

    [DockerFact]
    public async Task Tick_SkipsRowLockedByAnotherTransaction()
    {
        var locked = await CreateAsync();
        var available = await CreateAsync(recipient: 123002);
        await using var transaction = await Db.Database.BeginTransactionAsync();
        await Db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM notification_dispatches WHERE id = {locked.Id} FOR UPDATE");
        var telegram = new StubTelegram();
        await using var provider = CreateProvider(telegram);
        await TickAsync(provider).WaitAsync(TimeSpan.FromSeconds(10));
        telegram.Calls.Should().Be(1);
        (await ReadAsync(available.Id)).Status.Should().Be("sent");
        await transaction.RollbackAsync();
        await TickAsync(provider);
        telegram.Calls.Should().Be(2);
    }

    [DockerFact]
    public async Task Tick_MissesOnlyRowsOlderThanSixtyMinutes()
    {
        var expired = await CreateAsync();
        expired.DueAt = Clock.GetCurrentInstant() - Duration.FromMinutes(60) - Duration.FromTicks(10);
        var boundary = await CreateAsync(recipient: 123002);
        boundary.DueAt = Clock.GetCurrentInstant() - Duration.FromMinutes(60);
        await Db.SaveChangesAsync();
        var telegram = new StubTelegram();
        await using var provider = CreateProvider(telegram);
        await TickAsync(provider);
        (await ReadAsync(expired.Id)).Status.Should().Be("missed");
        (await ReadAsync(boundary.Id)).Status.Should().Be("sent");
        telegram.Calls.Should().Be(1);
    }

    [DockerFact]
    public async Task RejectedTransient_UsesBackoffAndFailsAfterThreeAttempts()
    {
        var dispatch = await CreateAsync();
        var telegram = new StubTelegram(() => throw new TelegramDeliveryException(TelegramDeliveryFailure.RejectedTransient));
        await using var provider = CreateProvider(telegram);
        await TickAsync(provider);
        await TickAsync(provider);
        telegram.Calls.Should().Be(1);
        Clock.Advance(Duration.FromSeconds(30));
        await TickAsync(provider);
        Clock.Advance(Duration.FromSeconds(59));
        await TickAsync(provider);
        telegram.Calls.Should().Be(2);
        Clock.Advance(Duration.FromSeconds(1));
        await TickAsync(provider);
        var failed = await ReadAsync(dispatch.Id);
        failed.Status.Should().Be("failed");
        failed.Attempts.Should().Be(3);
        failed.DueAt.Should().Be(dispatch.DueAt);
        await TickAsync(provider);
        telegram.Calls.Should().Be(3);
    }

    [DockerFact]
    public async Task RetryAfter_IsRespected_AndOriginalDueTimeStillExpires()
    {
        var dispatch = await CreateAsync();
        var telegram = new StubTelegram(() => throw new TelegramDeliveryException(TelegramDeliveryFailure.RejectedTransient, 7200));
        await using var provider = CreateProvider(telegram);
        await TickAsync(provider);
        Clock.Advance(Duration.FromMinutes(30));
        await TickAsync(provider);
        telegram.Calls.Should().Be(1);
        Clock.Advance(Duration.FromMinutes(31));
        await TickAsync(provider);
        (await ReadAsync(dispatch.Id)).Status.Should().Be("missed");
        telegram.Calls.Should().Be(1);
    }

    [DockerFact]
    public async Task AmbiguousTransportFailure_IsNeverRetried()
    {
        var dispatch = await CreateAsync();
        var telegram = new StubTelegram(() => throw new HttpRequestException("https://telegram.invalid/botSECRET"));
        await using var provider = CreateProvider(telegram);
        await TickAsync(provider);
        Clock.Advance(Duration.FromMinutes(3));
        await TickAsync(provider);
        var failed = await ReadAsync(dispatch.Id);
        failed.Status.Should().Be("failed");
        failed.Reason.Should().Be("delivery_unknown");
        failed.Attempts.Should().Be(1);
        telegram.Calls.Should().Be(1);
    }

    [DockerFact]
    public async Task AbandonedSendingClaim_IsFailedWithoutResend_WhileRecentClaimIsUntouched()
    {
        var old = await CreateAsync();
        old.Status = "sending";
        old.ClaimedAt = Clock.GetCurrentInstant() - Duration.FromMinutes(2);
        old.Attempts = 1;
        var recent = await CreateAsync(recipient: 123002);
        recent.Status = "sending";
        recent.ClaimedAt = Clock.GetCurrentInstant();
        recent.Attempts = 1;
        await Db.SaveChangesAsync();
        var telegram = new StubTelegram();
        await using var provider = CreateProvider(telegram);
        await TickAsync(provider);
        (await ReadAsync(old.Id)).Status.Should().Be("failed");
        (await ReadAsync(recent.Id)).Status.Should().Be("sending");
        telegram.Calls.Should().Be(0);
    }

    [DockerFact]
    public async Task BotBlocked_DisablesLearnerMessaging_AndSkipsFollowingDispatch()
    {
        var root = await ReportsPublicationTestData.CreateRootAsync(Db, Clock);
        var dispatch = await CreateAsync(recipient: root.Learner.TelegramUserId, learnerId: root.Learner.Id);
        var following = await CreateAsync("report_outcomes", root.Learner.TelegramUserId, root.Learner.Id);
        following.DueAt += Duration.FromSeconds(1);
        await Db.SaveChangesAsync();
        var telegram = new StubTelegram(() => throw new TelegramDeliveryException(TelegramDeliveryFailure.BotBlocked));
        await using var provider = CreateProvider(telegram);
        await TickAsync(provider);
        Clock.Advance(Duration.FromSeconds(1));
        await TickAsync(provider);
        (await ReadAsync(dispatch.Id)).Status.Should().Be("failed");
        (await ReadAsync(following.Id)).Reason.Should().Be("messaging_disabled");
        (await Db.Learners.AsNoTracking().SingleAsync()).CanMessage.Should().BeFalse();
        telegram.Calls.Should().Be(1);
    }

    [DockerFact]
    public async Task MissingHandler_AndEmptyPayload_NeverSend()
    {
        var unknown = await CreateAsync("unknown");
        var empty = await CreateAsync();
        var telegram = new StubTelegram();
        await using var provider = CreateProvider(telegram, new Handler("reminder", _ => new NotificationPreparation.Deliver(new NotificationMessage(" "))));
        await TickAsync(provider);
        (await ReadAsync(unknown.Id)).Reason.Should().Be("kind_not_registered");
        (await ReadAsync(empty.Id)).Reason.Should().Be("invalid_preparation");
        telegram.Calls.Should().Be(0);
    }

    [DockerFact]
    public async Task Handler_CanDefer_AndConditionsAreRecheckedAtNewDueTime()
    {
        var dispatch = await CreateAsync();
        var due = Clock.GetCurrentInstant() + Duration.FromMinutes(30);
        var handler = new Handler("reminder", _ => Clock.GetCurrentInstant() < due
            ? new NotificationPreparation.Defer(due)
            : new NotificationPreparation.Skip("session_completed"));
        var telegram = new StubTelegram();
        await using var provider = CreateProvider(telegram, handler);
        await TickAsync(provider);
        var deferred = await ReadAsync(dispatch.Id);
        deferred.Status.Should().Be("pending");
        deferred.DueAt.Should().Be(due);
        deferred.Attempts.Should().Be(0);
        Clock.Advance(Duration.FromMinutes(30));
        await TickAsync(provider);
        (await ReadAsync(dispatch.Id)).Reason.Should().Be("session_completed");
        telegram.Calls.Should().Be(0);
    }

    [DockerFact]
    public async Task FailedAndMissedSummaries_ReleaseReports_AndReleaseFailureRollsBackStatus()
    {
        var root = await ReportsPublicationTestData.CreateRootAsync(Db, Clock);
        var dispatch = await CreateAsync("report_outcomes", root.Learner.TelegramUserId, root.Learner.Id);
        var report = ReportsPublicationTestData.NewReport(root);
        report.SummaryDispatchId = dispatch.Id;
        Db.ContentReports.Add(report);
        await Db.SaveChangesAsync();
        var telegram = new StubTelegram(() => throw new TelegramDeliveryException(TelegramDeliveryFailure.RejectedPermanent));
        await using (var provider = CreateProvider(telegram))
        {
            await TickAsync(provider);
        }
        (await ReadAsync(dispatch.Id)).Status.Should().Be("failed");
        (await Db.ContentReports.AsNoTracking().SingleAsync()).SummaryDispatchId.Should().BeNull();

        Db.ChangeTracker.Clear();
        dispatch = await Db.NotificationDispatches.SingleAsync();
        dispatch.Status = "pending";
        dispatch.DueAt = Clock.GetCurrentInstant() - Duration.FromMinutes(61);
        report = await Db.ContentReports.SingleAsync();
        report.SummaryDispatchId = dispatch.Id;
        await Db.SaveChangesAsync();
        await using (var provider = CreateProvider(telegram, failingRelease: true))
        {
            var action = () => TickAsync(provider);
            await action.Should().ThrowAsync<InvalidOperationException>();
        }
        (await ReadAsync(dispatch.Id)).Status.Should().Be("pending");
        (await Db.ContentReports.AsNoTracking().SingleAsync()).SummaryDispatchId.Should().Be(dispatch.Id);
        await using (var provider = CreateProvider(telegram))
        {
            await TickAsync(provider);
        }
        (await ReadAsync(dispatch.Id)).Status.Should().Be("missed");
        (await Db.ContentReports.AsNoTracking().SingleAsync()).SummaryDispatchId.Should().BeNull();
        telegram.Calls.Should().Be(1);
    }

    private async Task<NotificationDispatch> CreateAsync(string kind = "reminder", long recipient = 123001, Guid? learnerId = null)
    {
        var dispatch = new NotificationDispatch
        {
            RecipientTelegramUserId = recipient,
            LearnerId = learnerId,
            Kind = kind,
            LocalDate = Clock.GetCurrentInstant().InUtc().Date,
            DueAt = Clock.GetCurrentInstant(),
            Status = "pending"
        };
        Db.NotificationDispatches.Add(dispatch);
        await Db.SaveChangesAsync();
        return dispatch;
    }

    private Task<NotificationDispatch> ReadAsync(Guid id) =>
        Db.NotificationDispatches.AsNoTracking().SingleAsync(dispatch => dispatch.Id == id);

    private ServiceProvider CreateProvider(StubTelegram telegram, Handler? handler = null, bool failingRelease = false)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = _postgres.ConnectionString
        }).Build();
        var services = new ServiceCollection().AddLogging().AddSingleton<IClock>(Clock)
            .AddPlatform(configuration).AddIdentity(configuration).AddContent()
            .AddNotifications().AddSingleton<ITelegramClient>(telegram)
            .AddSingleton<INotificationHandler>(handler ?? new Handler("reminder"))
            .AddSingleton<INotificationHandler>(new Handler("report_outcomes"));
        if (failingRelease)
        {
            services.AddScoped<IReportSummaryDispatchService, FailingRelease>();
        }
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static async Task TickAsync(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<NotificationDispatcher>().TickAsync();
    }

    private sealed class Handler(string kind, Func<NotificationDispatch, NotificationPreparation>? prepare = null) : INotificationHandler
    {
        public string Kind => kind;
        public Task<NotificationPreparation> PrepareAsync(NotificationDispatch dispatch, CancellationToken cancellationToken) =>
            Task.FromResult(prepare?.Invoke(dispatch) ?? new NotificationPreparation.Deliver(new NotificationMessage("Тестове повідомлення")));
    }

    private sealed class StubTelegram(Func<Task>? send = null) : ITelegramClient
    {
        private int _calls;
        public int Calls => _calls;
        public async Task SendMessageAsync(long chatId, string text, ParseMode parseMode = ParseMode.None,
            InlineKeyboardMarkup? replyMarkup = null, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _calls);
            if (send is not null)
            {
                await send();
            }
        }
        public Task ConfigureWebhookAsync(string url, string secretToken, IReadOnlyCollection<UpdateType> allowedUpdates,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FailingRelease(AppDbContext db) : IReportSummaryDispatchService
    {
        public async Task ReleaseAsync(Guid dispatchId, CancellationToken cancellationToken)
        {
            await db.ContentReports.Where(report => report.SummaryDispatchId == dispatchId)
                .ExecuteUpdateAsync(update => update.SetProperty(report => report.SummaryDispatchId, (Guid?)null), cancellationToken);
            throw new InvalidOperationException("Тестовий збій очищення підсумку.");
        }
    }
}
