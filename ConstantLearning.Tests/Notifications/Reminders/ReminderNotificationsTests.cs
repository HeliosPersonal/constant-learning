using System.Collections.Concurrent;
using System.Net;
using System.Text;
using ConstantLearning.Features.Content;
using ConstantLearning.Features.Content.Data;
using ConstantLearning.Features.Identity;
using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Features.Identity.LearnerDays;
using ConstantLearning.Features.Identity.NotificationAudience;
using ConstantLearning.Features.Identity.Settings;
using ConstantLearning.Features.Learning;
using ConstantLearning.Features.Learning.Data;
using ConstantLearning.Features.Learning.Notifications;
using ConstantLearning.Features.Notifications;
using ConstantLearning.Features.Notifications.Data;
using ConstantLearning.Features.Notifications.Dispatch;
using ConstantLearning.Features.Notifications.Reminders;
using ConstantLearning.Platform;
using ConstantLearning.Platform.Persistence;
using ConstantLearning.Platform.Telegram;
using ConstantLearning.Tests.Content.Reports;
using ConstantLearning.Tests.Infrastructure;
using ConstantLearning.Tests.Learning.Today;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Quartz;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using IdentityReminder = ConstantLearning.Features.Identity.Data.ReminderSchedule;

namespace ConstantLearning.Tests.Notifications.Reminders;

public sealed class ReminderNotificationsTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;
    private const long TelegramId = 900006001;
    private static readonly LocalDate Day = new(2026, 11, 2);

    [DockerFact]
    public async Task Ac26_NoAnswers_NewLesson_ParallelPlanningDispatchAndRestartSendExactlyOnce()
    {
        Clock.Reset(Instant.FromUtc(2026, 11, 2, 17, 59));
        await LearnerAsync();
        await TodayTestData.CourseAsync(Db, 1);
        var telegram = new StubTelegram();
        await using (var first = Provider(telegram))
        {
            await Task.WhenAll(PlanAsync(first), PlanAsync(first));
            await DispatchAsync(first);
            Assert.Empty(telegram.Messages);
        }
        Assert.Single(await Db.NotificationDispatches.ToArrayAsync());
        Clock.Advance(Duration.FromMinutes(1));
        await using var restarted = Provider(telegram);
        await Task.WhenAll(DispatchAsync(restarted), DispatchAsync(restarted));
        await PlanAsync(restarted);
        await DispatchAsync(restarted);
        var message = Assert.Single(telegram.Messages);
        Assert.Contains("щоденного заняття", message.Text, StringComparison.Ordinal);
        Assert.Equal("Відкрити заняття", message.Markup!.InlineKeyboard.Single().Single().Text);
        Assert.Equal("https://trainer.example/", message.Markup.InlineKeyboard.Single().Single().WebApp!.Url);
        Assert.Equal("sent", (await DispatchRowAsync()).Status);
    }

    [DockerFact]
    public async Task Ac26_CompletedSession_SkipsEvenWhenMoreNewWordsAreAvailable()
    {
        AtReminder();
        var learner = await LearnerAsync();
        await TodayTestData.CourseAsync(Db, 1);
        await SessionAsync(learner, "completed");
        await AssertSkippedAsync("reminder_session_completed");
    }

    [DockerFact]
    public async Task Ac26_NoDueWordsAndCourseCompleted_Skips()
    {
        AtReminder();
        var learner = await LearnerAsync();
        var course = await TodayTestData.CourseAsync(Db, 1);
        Db.LearnerWords.Add(Word(learner, course[0], Day.PlusDays(-1), Day.PlusDays(1)));
        await Db.SaveChangesAsync();
        await AssertSkippedAsync("reminder_no_words");
    }

    [DockerFact]
    public async Task Ac26_ExhaustedDailyLimit_SkipsEvenWithUnseenCourseWords()
    {
        AtReminder();
        var learner = await LearnerAsync();
        var course = await TodayTestData.CourseAsync(Db, 6);
        Db.LearnerWords.AddRange(course.Take(5).Select(entry => Word(learner, entry, Day, Day.PlusDays(1))));
        await Db.SaveChangesAsync();
        await AssertSkippedAsync("reminder_no_words");
    }

    [DockerFact]
    public async Task DueReview_AllowsReminderWhenNewWordsGateIsClosed()
    {
        AtReminder();
        var learner = await LearnerAsync();
        var course = await TodayTestData.CourseAsync(Db, 1);
        Db.LearnerWords.Add(Word(learner, course[0], Day.PlusDays(-1), Day));
        await Db.SaveChangesAsync();
        await Db.Learners.ExecuteUpdateAsync(update => update.SetProperty(row => row.NewWordsAvailableFrom, Clock.GetCurrentInstant() + Duration.FromDays(1)));
        var telegram = new StubTelegram();
        await using var provider = Provider(telegram);
        await PlanAsync(provider);
        await DispatchAsync(provider);
        Assert.Single(telegram.Messages);
    }

    [DockerFact]
    public async Task Ac26_RecentAnswer_DefersRepeatedlyBeyondOriginal60Minutes_AndPlannerAndRestartKeepPause()
    {
        AtReminder();
        var learner = await LearnerAsync();
        var course = await TodayTestData.CourseAsync(Db, 2);
        var answer = await AnswerAsync(learner, course[0], Clock.GetCurrentInstant() - Duration.FromMinutes(10));
        await SessionAsync(learner);
        var telegram = new StubTelegram();
        await using (var first = Provider(telegram))
        {
            await PlanAsync(first);
            await DispatchAsync(first);
            Assert.Equal(Instant.FromUtc(2026, 11, 2, 18, 20), (await DispatchRowAsync()).DueAt);
            await PlanAsync(first);
            Assert.Equal(Instant.FromUtc(2026, 11, 2, 18, 20), (await DispatchRowAsync()).DueAt);
        }
        await using var restarted = Provider(telegram);
        for (var minute = 20; minute <= 80; minute += 20)
        {
            Clock.Reset(Instant.FromUtc(2026, 11, 2, 18, 0) + Duration.FromMinutes(minute));
            await Db.AnswerEvents.Where(row => row.Id == answer.Id).ExecuteUpdateAsync(update =>
                update.SetProperty(row => row.AnsweredAt, Clock.GetCurrentInstant() - Duration.FromMinutes(10)));
            await DispatchAsync(restarted);
            Assert.Empty(telegram.Messages);
            Assert.Equal(0, (await DispatchRowAsync()).Attempts);
        }
        Clock.Reset(Instant.FromUtc(2026, 11, 2, 19, 40));
        await PlanAsync(restarted);
        await DispatchAsync(restarted);
        var message = Assert.Single(telegram.Messages);
        Assert.Contains("продовжити", message.Text, StringComparison.Ordinal);
        Assert.Equal("Продовжити заняття", message.Markup!.InlineKeyboard.Single().Single().Text);
        Assert.Equal(1, (await DispatchRowAsync()).Attempts);
    }

    [DockerFact]
    public async Task Ac26_ThirtyMinutePauseAlreadyElapsed_ResumesImmediately()
    {
        AtReminder();
        var learner = await LearnerAsync();
        var course = await TodayTestData.CourseAsync(Db, 2);
        await AnswerAsync(learner, course[0], Clock.GetCurrentInstant() - Duration.FromMinutes(30));
        var telegram = new StubTelegram();
        await using var provider = Provider(telegram);
        await PlanAsync(provider);
        await DispatchAsync(provider);
        Assert.Contains("продовжити", Assert.Single(telegram.Messages).Text, StringComparison.Ordinal);
    }

    [DockerFact]
    public async Task Ac26_AcknowledgedNewCardWithoutAnswerEvent_DefersAndResumes()
    {
        AtReminder();
        var learner = await LearnerAsync();
        var course = await TodayTestData.CourseAsync(Db, 2);
        var session = await SessionAsync(learner);
        Db.SessionItems.Add(new SessionItem
        {
            SessionId = session.Id,
            EntryId = course[0].Id,
            Position = 1,
            Kind = "new_word_card",
            State = "answered",
            AnsweredAt = Clock.GetCurrentInstant() - Duration.FromMinutes(5),
            CreatedAt = Clock.GetCurrentInstant() - Duration.FromMinutes(10)
        });
        Db.LearnerWords.Add(Word(learner, course[0], Day, Day.PlusDays(1)));
        await Db.SaveChangesAsync();
        Assert.Empty(await Db.AnswerEvents.ToArrayAsync());
        var telegram = new StubTelegram();
        await using var provider = Provider(telegram);
        await PlanAsync(provider);
        await DispatchAsync(provider);
        Assert.Empty(telegram.Messages);
        Assert.Equal(Instant.FromUtc(2026, 11, 2, 18, 25), (await DispatchRowAsync()).DueAt);
        Clock.Advance(Duration.FromMinutes(25));
        await DispatchAsync(provider);
        Assert.Contains("продовжити", Assert.Single(telegram.Messages).Text, StringComparison.Ordinal);
    }

    [DockerFact]
    public async Task Ac26_NewSessionAndSkippedCardWithoutProcessedCards_UsesNewLessonText()
    {
        AtReminder();
        var learner = await LearnerAsync();
        var course = await TodayTestData.CourseAsync(Db, 2);
        var session = await SessionAsync(learner);
        await Db.LearningSessions.ExecuteUpdateAsync(update => update.SetProperty(row => row.LastActivityAt, Clock.GetCurrentInstant()));
        Db.SessionItems.Add(new SessionItem
        {
            SessionId = session.Id,
            EntryId = course[0].Id,
            Position = 1,
            Kind = "new_word_card",
            State = "skipped",
            AnsweredAt = Clock.GetCurrentInstant(),
            CreatedAt = Clock.GetCurrentInstant()
        });
        await Db.SaveChangesAsync();
        var telegram = new StubTelegram();
        await using var provider = Provider(telegram);
        await PlanAsync(provider);
        await DispatchAsync(provider);
        Assert.Contains("щоденного заняття", Assert.Single(telegram.Messages).Text, StringComparison.Ordinal);
    }

    [DockerFact]
    public async Task Ac26_CompletionDuringPause_SkipsInsteadOfSending()
    {
        AtReminder();
        var learner = await LearnerAsync();
        var course = await TodayTestData.CourseAsync(Db, 2);
        await AnswerAsync(learner, course[0], Clock.GetCurrentInstant() - Duration.FromMinutes(10));
        var session = await SessionAsync(learner);
        var telegram = new StubTelegram();
        await using var provider = Provider(telegram);
        await PlanAsync(provider);
        await DispatchAsync(provider);
        await Db.LearningSessions.Where(row => row.Id == session.Id).ExecuteUpdateAsync(update =>
            update.SetProperty(row => row.Status, "completed").SetProperty(row => row.CompletedAt, Clock.GetCurrentInstant()));
        Clock.Advance(Duration.FromMinutes(20));
        await DispatchAsync(provider);
        Assert.Empty(telegram.Messages);
        Assert.Equal("reminder_session_completed", (await DispatchRowAsync()).Reason);
    }

    [DockerFact]
    public async Task Ac26_ContinuedActivity_ReachesTwoHourBoundaryAndSendsWithoutFurtherDeferral()
    {
        AtReminder();
        var learner = await LearnerAsync();
        var course = await TodayTestData.CourseAsync(Db, 2);
        var answer = await AnswerAsync(learner, course[0], Clock.GetCurrentInstant());
        var telegram = new StubTelegram();
        await using var provider = Provider(telegram);
        await PlanAsync(provider);
        await DispatchAsync(provider);
        for (var minute = 30; minute <= 120; minute += 30)
        {
            Clock.Reset(Instant.FromUtc(2026, 11, 2, 18, 0) + Duration.FromMinutes(minute));
            await Db.AnswerEvents.Where(row => row.Id == answer.Id).ExecuteUpdateAsync(update => update.SetProperty(row => row.AnsweredAt, Clock.GetCurrentInstant()));
            await DispatchAsync(provider);
        }
        Assert.Single(telegram.Messages);
        Assert.Equal("sent", (await DispatchRowAsync()).Status);
        Assert.Equal(Instant.FromUtc(2026, 11, 2, 20, 0), (await DispatchRowAsync()).DueAt);
    }

    [DockerFact]
    public async Task Ac26_PauseReachesMidnight_SkipsRatherThanSendingDuringNextDay()
    {
        Clock.Reset(Instant.FromUtc(2026, 11, 2, 22, 45));
        var learner = await LearnerAsync(new LocalTime(23, 45));
        var course = await TodayTestData.CourseAsync(Db, 2);
        await AnswerAsync(learner, course[0], Clock.GetCurrentInstant() - Duration.FromMinutes(15));
        var telegram = new StubTelegram();
        await using var provider = Provider(telegram);
        await PlanAsync(provider);
        await DispatchAsync(provider);
        Assert.Equal(Instant.FromUtc(2026, 11, 2, 23, 0), (await DispatchRowAsync()).DueAt);
        Clock.Advance(Duration.FromMinutes(15));
        await DispatchAsync(provider);
        Assert.Empty(telegram.Messages);
        Assert.Equal("reminder_day_changed", (await DispatchRowAsync()).Reason);
    }

    [DockerFact]
    public async Task MissedOver60Minutes_IsNotDelivered_And429RetainsDeadlineAndAttempts()
    {
        AtReminder();
        await LearnerAsync();
        await TodayTestData.CourseAsync(Db, 1);
        var telegram = new StubTelegram(attempt => attempt == 1
            ? new TelegramDeliveryException(TelegramDeliveryFailure.RejectedTransient, 45) : null);
        await using var provider = Provider(telegram);
        await PlanAsync(provider);
        await DispatchAsync(provider);
        var first = await DispatchRowAsync();
        Clock.Advance(Duration.FromSeconds(44));
        await PlanAsync(provider);
        await DispatchAsync(provider);
        Assert.Single(telegram.Messages);
        Clock.Advance(Duration.FromSeconds(1));
        await DispatchAsync(provider);
        Assert.Equal(2, telegram.Messages.Count);
        Assert.Equal(first.DueAt, (await DispatchRowAsync()).DueAt);
        Assert.Equal(2, (await DispatchRowAsync()).Attempts);
        Clock.Advance(Duration.FromDays(1));
        await PlanAsync(provider);
        Clock.Advance(Duration.FromMinutes(61));
        await DispatchAsync(provider);
        Assert.Equal(2, telegram.Messages.Count);
        Assert.Equal("missed", (await Db.NotificationDispatches.AsNoTracking().SingleAsync(row => row.LocalDate == Day.PlusDays(1))).Status);
    }

    [DockerFact]
    public async Task Ac44_HmacSettings_ReplansPending_AndWestwardExtendedDayNeverSendsTwice()
    {
        Clock.Reset(Instant.FromUtc(2026, 11, 2, 17, 0));
        await LearnerAsync();
        await TodayTestData.CourseAsync(Db, 1);
        var telegram = new StubTelegram();
        await using var provider = Provider(telegram);
        await PlanAsync(provider);
        await using var app = await ReportsTestApplication.StartAsync(_postgres.ConnectionString, Clock,
            services => services.AddNotifications());
        await PatchSettingsAsync(app, "{\"time_zone\":\"America/New_York\"}");
        Assert.Equal(Instant.FromUtc(2026, 11, 3, 0, 0), (await DispatchRowAsync()).DueAt);
        Assert.Equal(Day, (await DispatchRowAsync()).LocalDate);
        Clock.Reset(Instant.FromUtc(2026, 11, 3, 0, 0));
        await DispatchAsync(provider);
        Assert.Single(telegram.Messages);
        await PatchSettingsAsync(app, "{\"time_zone\":\"Pacific/Honolulu\"}");
        Clock.Reset(Instant.FromUtc(2026, 11, 3, 5, 0));
        await PlanAsync(provider);
        await DispatchAsync(provider);
        Assert.Single(telegram.Messages);
        Assert.Single(await Db.NotificationDispatches.ToArrayAsync());
    }

    [DockerFact]
    public async Task SettingsDisableThenEnable_ReusesUnsentRow_AndSelectedWeekdaysAreRespected()
    {
        Clock.Reset(Instant.FromUtc(2026, 11, 2, 17, 0));
        await LearnerAsync();
        await TodayTestData.CourseAsync(Db, 1);
        var telegram = new StubTelegram();
        await using var provider = Provider(telegram);
        await PlanAsync(provider);
        var original = await DispatchRowAsync();
        await using var app = await ReportsTestApplication.StartAsync(_postgres.ConnectionString, Clock,
            services => services.AddNotifications());
        await PatchSettingsAsync(app, "{\"reminder\":null}");
        Assert.Equal("skipped", (await DispatchRowAsync()).Status);
        await PatchSettingsAsync(app, "{\"reminder\":{\"local_time\":\"19:00\",\"days_of_week\":[\"tue\"]}}");
        Assert.Equal("skipped", (await DispatchRowAsync()).Status);
        await PatchSettingsAsync(app, "{\"reminder\":{\"local_time\":\"19:00\",\"days_of_week\":[\"mon\"]}}");
        Assert.Equal(original.Id, (await DispatchRowAsync()).Id);
        Assert.Equal("pending", (await DispatchRowAsync()).Status);
        Clock.Advance(Duration.FromHours(1));
        await DispatchAsync(provider);
        Assert.Single(telegram.Messages);
    }

    [ReminderDockerTheory]
    [InlineData(2026, 3, 29, 1, 30)]
    [InlineData(2026, 10, 25, 0, 30)]
    public async Task DstSkippedAndAmbiguousTime_UsesOneResolvedOccurrence(int year, int month, int day, int hour, int minute)
    {
        Clock.Reset(Instant.FromUtc(year, month, day, 0, 0));
        await LearnerAsync(new LocalTime(2, 30));
        await TodayTestData.CourseAsync(Db, 1);
        var telegram = new StubTelegram();
        await using var provider = Provider(telegram);
        await PlanAsync(provider);
        var due = Instant.FromUtc(year, month, day, hour, minute);
        Assert.Equal(due, (await DispatchRowAsync()).DueAt);
        Clock.Reset(due);
        await DispatchAsync(provider);
        Clock.Advance(Duration.FromHours(1));
        await PlanAsync(provider);
        await DispatchAsync(provider);
        Assert.Single(telegram.Messages);
        Assert.Single(await Db.NotificationDispatches.ToArrayAsync());
    }

    [DockerFact]
    public async Task LearningPort_ReadsPendingCurrentSession_AndDoesNotMutateExpiredSession()
    {
        AtReminder();
        var learner = await LearnerAsync();
        var course = await TodayTestData.CourseAsync(Db, 6);
        Db.LearnerWords.AddRange(course.Take(5).Select(entry => Word(learner, entry, Day, Day.PlusDays(1))));
        var session = await SessionAsync(learner);
        Db.SessionItems.Add(new SessionItem
        {
            SessionId = session.Id,
            EntryId = course[0].Id,
            Position = 1,
            Kind = "new_word_check",
            State = "pending",
            CreatedAt = Clock.GetCurrentInstant()
        });
        await Db.SaveChangesAsync();
        await using var provider = Provider(new StubTelegram());
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IReminderLearningReader>();
        Assert.True((await reader.ReadAsync(learner.Id, "pl", Day, 5, null)).HasWork);
        Assert.Null((await reader.ReadAsync(learner.Id, "pl", Day, 5, null)).LastAnsweredAt);
        await Db.LearningSessions.ExecuteUpdateAsync(update => update.SetProperty(row => row.EndsAt, Clock.GetCurrentInstant()));
        Assert.False((await reader.ReadAsync(learner.Id, "pl", Day, 5, null)).HasWork);
        Assert.Equal("active", (await Db.LearningSessions.AsNoTracking().SingleAsync()).Status);
    }

    [DockerFact]
    public async Task DispatcherHoldingDispatch_CanReadWhileSettingsHoldLearner_WithoutLockCycle()
    {
        AtReminder();
        var learner = await LearnerAsync();
        await TodayTestData.CourseAsync(Db, 1);
        await using var provider = Provider(new StubTelegram());
        await PlanAsync(provider);
        await using var claimScope = provider.CreateAsyncScope();
        var db = claimScope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var claim = await db.Database.BeginTransactionAsync();
        var row = await db.NotificationDispatches.FromSqlRaw("SELECT *, xmin FROM notification_dispatches FOR UPDATE").SingleAsync();
        var saved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var updating = ChangeZoneAsync(provider, learner.Id, saved);
        try
        {
            await saved.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var prepared = await claimScope.ServiceProvider.GetServices<INotificationHandler>().Single(handler => handler.Kind == "reminder")
                .PrepareAsync(row, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsType<NotificationPreparation.Deliver>(prepared);
        }
        finally { await claim.CommitAsync(); }
        await updating.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(Instant.FromUtc(2026, 11, 3, 0, 0), (await DispatchRowAsync()).DueAt);
    }

    [DockerFact]
    public async Task PlannerHoldingLearnerKeyShare_CompletesBeforeSettingsAndCannotOverwriteNewTimezone()
    {
        AtReminder();
        var learner = await LearnerAsync();
        await TodayTestData.CourseAsync(Db, 1);
        var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var provider = Provider(new StubTelegram(), services =>
        {
            var descriptor = services.Single(value => value.ServiceType == typeof(INotificationAudienceReader));
            services.Remove(descriptor);
            services.AddScoped<INotificationAudienceReader>(scope => new PausedAudience(
                (INotificationAudienceReader)ActivatorUtilities.CreateInstance(scope, descriptor.ImplementationType!), locked, release));
        });
        var planning = PlanAsync(provider);
        Task? changing = null;
        try
        {
            await locked.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            changing = ChangeZoneAsync(provider, learner.Id, new TaskCompletionSource(), started);
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(changing.IsCompleted);
        }
        finally { release.TrySetResult(); }
        await planning.WaitAsync(TimeSpan.FromSeconds(5));
        await changing!.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(Instant.FromUtc(2026, 11, 3, 0, 0), (await DispatchRowAsync()).DueAt);
    }

    [DockerFact]
    public async Task SettingsChangeDuringLongPause_DoesNotResetDueAtToExpiredOriginalTime()
    {
        AtReminder();
        var learner = await LearnerAsync();
        var course = await TodayTestData.CourseAsync(Db, 2);
        var answer = await AnswerAsync(learner, course[0], Clock.GetCurrentInstant());
        var telegram = new StubTelegram();
        await using var provider = Provider(telegram);
        await PlanAsync(provider);
        await DispatchAsync(provider);
        for (var minute = 30; minute <= 90; minute += 30)
        {
            Clock.Reset(Instant.FromUtc(2026, 11, 2, 18, 0) + Duration.FromMinutes(minute));
            await Db.AnswerEvents.Where(row => row.Id == answer.Id).ExecuteUpdateAsync(update => update.SetProperty(row => row.AnsweredAt, Clock.GetCurrentInstant()));
            await DispatchAsync(provider);
        }
        await using var app = await ReportsTestApplication.StartAsync(_postgres.ConnectionString, Clock,
            services => services.AddNotifications());
        await PatchSettingsAsync(app, "{\"daily_new_words_limit\":10}");
        Assert.Equal(Instant.FromUtc(2026, 11, 2, 20, 0), (await DispatchRowAsync()).DueAt);
        Clock.Advance(Duration.FromMinutes(30));
        await DispatchAsync(provider);
        Assert.Single(telegram.Messages);
    }

    [DockerFact]
    public async Task PlannerIsRegisteredAsThirtySecondNonConcurrentJob()
    {
        await using var provider = Provider(new StubTelegram());
        await using var logging = new QuartzTestLoggingScope(provider);
        var scheduler = await logging.GetSchedulerAsync();
        var detail = await scheduler.GetJobDetail(new JobKey("ReminderPlanner"));
        Assert.Equal(typeof(ReminderPlannerJob), detail!.JobType);
        Assert.True(detail.ConcurrentExecutionDisallowed);
        var trigger = Assert.IsAssignableFrom<ISimpleTrigger>(await scheduler.GetTrigger(new TriggerKey("ReminderPlanner-trigger")));
        Assert.Equal(TimeSpan.FromSeconds(30), trigger.RepeatInterval);
    }

    private void AtReminder() => Clock.Reset(Instant.FromUtc(2026, 11, 2, 18, 0));

    [DockerFact]
    public async Task PerLanguageReminders_PlanEachLanguage_AndSettingsChangeTouchesOnlyItsLanguage()
    {
        // 05:00 UTC = 06:00 у Варшаві: обидва нагадування (en 08:00, pl 19:00) ще попереду сьогодні.
        Clock.Reset(Instant.FromUtc(2026, 11, 2, 5, 0));
        var learner = await LearnerAsync(new LocalTime(19, 0));
        Db.ReminderSchedules.Add(new IdentityReminder
        {
            LearnerId = learner.Id,
            TargetLanguage = "en",
            LocalTime = new LocalTime(8, 0),
            DaysOfWeek = 127,
            CreatedAt = Clock.GetCurrentInstant()
        });
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();

        await using var provider = Provider(new StubTelegram());
        await PlanAsync(provider);

        var rows = await Db.NotificationDispatches.AsNoTracking().Where(row => row.Kind == "reminder")
            .OrderBy(row => row.TargetLanguage).ToArrayAsync();
        Assert.Equal(["en", "pl"], rows.Select(row => row.TargetLanguage).ToArray());
        Assert.Equal(Instant.FromUtc(2026, 11, 2, 7, 0), rows[0].DueAt);
        Assert.Equal(Instant.FromUtc(2026, 11, 2, 18, 0), rows[1].DueAt);
        Assert.All(rows, row => Assert.Equal("pending", row.Status));

        // Вимкнення нагадування en (як PATCH з reminder=null) скасовує лише її сьогоднішнє сповіщення.
        await using (var scope = provider.CreateAsyncScope())
        {
            var scoped = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await using var transaction = await scoped.Database.BeginTransactionAsync();
            scoped.ReminderSchedules.Remove(await scoped.ReminderSchedules.SingleAsync(row => row.TargetLanguage == "en"));
            await scoped.SaveChangesAsync();
            await scope.ServiceProvider.GetServices<ILearnerSettingsChangedHandler>()
                .OfType<ReminderSettingsChangedHandler>().Single()
                .HandleAsync(new(learner.Id, Clock.GetCurrentInstant(), null!, "en", false), CancellationToken.None);
            await transaction.CommitAsync();
        }

        var statuses = await Db.NotificationDispatches.AsNoTracking().Where(row => row.Kind == "reminder")
            .ToDictionaryAsync(row => row.TargetLanguage!, row => (row.Status, row.Reason));
        Assert.Equal(("skipped", "reminder_not_scheduled_today"), statuses["en"]);
        Assert.Equal(("pending", null), statuses["pl"]);
    }

    private async Task<Learner> LearnerAsync(LocalTime? time = null)
    {
        var learner = new Learner
        {
            TelegramUserId = TelegramId,
            TimeZone = "Europe/Warsaw",
            TimeZoneConfirmed = true,
            DailyNewWordsLimit = 5,
            CanMessage = true,
            OnboardedAt = Clock.GetCurrentInstant(),
            CreatedAt = Clock.GetCurrentInstant()
        };
        learner.ReminderSchedules.Add(new IdentityReminder
        {
            Learner = learner,
            LearnerId = learner.Id,
            TargetLanguage = "pl",
            LocalTime = time ?? new LocalTime(19, 0),
            DaysOfWeek = 127,
            CreatedAt = Clock.GetCurrentInstant()
        });
        Db.Learners.Add(learner);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        return learner;
    }

    private async Task<LearningSession> SessionAsync(Learner learner, string status = "active")
    {
        var session = new LearningSession
        {
            LearnerId = learner.Id,
            LearnerDay = Day,
            Status = status,
            Kind = "plan",
            EndsAt = Instant.FromUtc(2026, 11, 2, 23, 0),
            CreatedAt = Clock.GetCurrentInstant(),
            CompletedAt = status == "completed" ? Clock.GetCurrentInstant() : null
        };
        Db.LearningSessions.Add(session);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        return session;
    }

    private async Task<AnswerEvent> AnswerAsync(Learner learner, DictionaryEntry entry, Instant at)
    {
        var answer = new AnswerEvent
        {
            LearnerId = learner.Id,
            EntryId = entry.Id,
            VersionId = entry.PublishedVersionId!.Value,
            LearnerDay = Day,
            AnsweredAt = at,
            ExerciseKind = "review",
            Outcome = "mistake_typo",
            TimeZone = learner.TimeZone,
            SchedulerVersion = "memorize-v1",
            StateBefore = "{}",
            CreatedAt = at
        };
        Db.AnswerEvents.Add(answer);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        return answer;
    }

    private static LearnerWord Word(Learner learner, DictionaryEntry entry, LocalDate shown, LocalDate due) => new()
    {
        LearnerId = learner.Id,
        EntryId = entry.Id,
        FirstShownDay = shown,
        DueOn = due,
        IntervalDays = 2,
        Status = "review",
        SchedulerVersion = "memorize-v1",
        ModelState = "{}"
    };

    private ServiceProvider Provider(StubTelegram telegram, Action<IServiceCollection>? configure = null)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = _postgres.ConnectionString,
            ["Telegram:BotToken"] = TelegramInitDataSigner.TestBotToken,
            ["Telegram:MiniAppUrl"] = "https://trainer.example/"
        }).Build();
        var services = new ServiceCollection().AddLogging().AddSingleton<IClock>(Clock).AddPlatform(config)
            .AddIdentity(config).AddLearning().AddContent().AddNotifications().AddSingleton<ITelegramClient>(telegram)
            .AddQuartz("reminders-" + Guid.NewGuid().ToString("N"), _ => { });
        configure?.Invoke(services);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static async Task PlanAsync(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ReminderPlanner>().TickAsync();
    }

    private static async Task DispatchAsync(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<NotificationDispatcher>().TickAsync();
    }

    private Task<NotificationDispatch> DispatchRowAsync() => Db.NotificationDispatches.AsNoTracking().SingleAsync();

    private async Task AssertSkippedAsync(string reason)
    {
        var telegram = new StubTelegram();
        await using var provider = Provider(telegram);
        await PlanAsync(provider);
        await DispatchAsync(provider);
        Assert.Empty(telegram.Messages);
        Assert.Equal("skipped", (await DispatchRowAsync()).Status);
        Assert.Equal(reason, (await DispatchRowAsync()).Reason);
    }

    private async Task PatchSettingsAsync(ReportsTestApplication app, string body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, "/api/v1/learners/me/settings")
        { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Authorization = new("tma", new TelegramInitDataSigner().Sign(TelegramId, Clock.GetCurrentInstant()));
        using var response = await app.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task ChangeZoneAsync(ServiceProvider provider, Guid learnerId, TaskCompletionSource saved, TaskCompletionSource? started = null)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var days = scope.ServiceProvider.GetRequiredService<ILearnerDayStateService>();
        started?.TrySetResult();
        var current = await days.GetForUpdateAsync(learnerId);
        var changed = days.ChangeTimeZone(current!, "America/New_York");
        await db.SaveChangesAsync();
        saved.TrySetResult();
        await scope.ServiceProvider.GetServices<ILearnerSettingsChangedHandler>().OfType<ReminderSettingsChangedHandler>().Single()
            .HandleAsync(new(learnerId, Clock.GetCurrentInstant(), changed.Day, "pl", true), CancellationToken.None);
        await transaction.CommitAsync();
    }

    private sealed record Sent(string Text, InlineKeyboardMarkup? Markup);
    private sealed class PausedAudience(INotificationAudienceReader inner, TaskCompletionSource locked,
        TaskCompletionSource release) : INotificationAudienceReader
    {
        public Task<IReadOnlyList<LearnerNotificationRecipient>> ReadLearnersAsync(IReadOnlyCollection<Guid> learnerIds,
            CancellationToken cancellationToken = default) => inner.ReadLearnersAsync(learnerIds, cancellationToken);
        public Task<LearnerNotificationRecipient?> ReadLearnerAsync(Guid learnerId, CancellationToken cancellationToken = default) =>
            inner.ReadLearnerAsync(learnerId, cancellationToken);
        public async Task<LearnerNotificationRecipient?> ReadForPlanningAsync(Guid learnerId, CancellationToken cancellationToken = default)
        {
            var result = await inner.ReadForPlanningAsync(learnerId, cancellationToken);
            locked.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
            return result;
        }
        public IReadOnlyList<long> ReadEditorTelegramIds() => inner.ReadEditorTelegramIds();
    }

    private sealed class StubTelegram(Func<int, TelegramDeliveryException?>? failure = null) : ITelegramClient
    {
        private int _attempts;
        public ConcurrentQueue<Sent> Messages { get; } = new();
        public Task SendMessageAsync(long chatId, string text, ParseMode parseMode = ParseMode.None,
            InlineKeyboardMarkup? replyMarkup = null, CancellationToken cancellationToken = default)
        {
            Assert.Equal(TelegramId, chatId);
            Assert.Equal(ParseMode.None, parseMode);
            Messages.Enqueue(new(text, replyMarkup));
            if (failure?.Invoke(Interlocked.Increment(ref _attempts)) is { } exception) throw exception;
            return Task.CompletedTask;
        }
        public Task ConfigureWebhookAsync(string url, string secretToken, IReadOnlyCollection<UpdateType> allowedUpdates,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}

public sealed class ReminderDockerTheoryAttribute : TheoryAttribute
{
    public ReminderDockerTheoryAttribute() => Skip = DockerAvailability.GetSkipReason(DockerAvailability.IsCi, DockerAvailability.UnavailableReason);
}
