using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ConstantLearning.Features.Content;
using ConstantLearning.Features.Content.Data;
using ConstantLearning.Features.Content.Notifications;
using ConstantLearning.Features.Identity;
using ConstantLearning.Features.Identity.Auth;
using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Features.Identity.LearnerDays;
using ConstantLearning.Features.Identity.NotificationAudience;
using ConstantLearning.Features.Identity.Settings;
using ConstantLearning.Features.Learning;
using ConstantLearning.Features.Notifications;
using ConstantLearning.Features.Notifications.Data;
using ConstantLearning.Features.Notifications.Dispatch;
using ConstantLearning.Features.Notifications.Summaries;
using ConstantLearning.Platform;
using ConstantLearning.Platform.Persistence;
using ConstantLearning.Platform.Telegram;
using ConstantLearning.Tests.Content.Reports;
using ConstantLearning.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using NodaTime.Text;
using Quartz;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace ConstantLearning.Tests.Notifications.Summaries;

public sealed class SummaryNotificationsTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;
    private const long LearnerTelegramId = 900005001;
    private const long EditorTelegramId = 900005002;

    [DockerFact]
    public async Task Ac37_NoonSummary_ContainsEveryOwnOutcome_AndNeverRepeatsSentResults()
    {
        Clock.Reset(Instant.FromUtc(2026, 11, 2, 10, 59));
        var learner = await LearnerAsync();
        var reports = new[]
        {
            await ClosedAsync(learner, "kot", "applied"),
            await ClosedAsync(learner, "pies", "added_to_candidates"),
            await ClosedAsync(learner, "dom", "rejected", "source_not_confirming")
        };
        var other = await LearnerAsync(telegramId: LearnerTelegramId + 10);
        await ClosedAsync(other, "cudzy", "rejected", "already_correct");
        var telegram = new StubTelegram();
        await using var provider = Provider(telegram);
        await PlanAsync(provider);
        await DispatchAsync(provider);
        Assert.Empty(telegram.Messages);
        Clock.Advance(Duration.FromMinutes(1));
        await DispatchAsync(provider);
        var message = Assert.Single(telegram.Messages, item => item.Recipient == LearnerTelegramId);
        Assert.Equal(ParseMode.None, message.ParseMode);
        Assert.Contains("kot (відсутнє слово): Виправлено.", message.Text, StringComparison.Ordinal);
        Assert.Contains("pies (відсутнє слово): Додано до кандидатів.", message.Text, StringComparison.Ordinal);
        Assert.Contains("dom (відсутнє слово): Не підтверджено: джерело не підтверджує.", message.Text, StringComparison.Ordinal);
        Assert.All(reports, report => Assert.DoesNotContain(report.Id.ToString(), message.Text, StringComparison.Ordinal));
        Assert.DoesNotContain("cudzy", message.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE", message.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("R-", message.Text, StringComparison.Ordinal);
        var dispatch = await Db.NotificationDispatches.AsNoTracking().SingleAsync(row => row.LearnerId == learner.Id);
        Assert.Equal("sent", dispatch.Status);
        Assert.All(await Db.ContentReports.Where(row => row.LearnerId == learner.Id).ToArrayAsync(), row => Assert.Equal(dispatch.Id, row.SummaryDispatchId));
        AssertEvent(dispatch);
        Clock.Advance(Duration.FromDays(1));
        await PlanAsync(provider);
        await DispatchAsync(provider);
        Assert.Single(telegram.Messages, item => item.Recipient == LearnerTelegramId);
        await ClosedAsync(learner, "nowy", "rejected", "already_correct", Clock.GetCurrentInstant() - Duration.FromMinutes(1));
        await PlanAsync(provider);
        await DispatchAsync(provider);
        Assert.Equal(2, telegram.Messages.Count(item => item.Recipient == LearnerTelegramId));
        var next = telegram.Messages.Last(item => item.Recipient == LearnerTelegramId);
        Assert.Contains("nowy", next.Text, StringComparison.Ordinal);
        Assert.All(reports, report => Assert.DoesNotContain(report.SearchQuery!, next.Text, StringComparison.Ordinal));
        var previousIds = reports.Select(report => report.Id).ToArray();
        Assert.Equal(3, await Db.ContentReports.CountAsync(report => previousIds.Contains(report.Id) && report.SummaryDispatchId == dispatch.Id));
    }

    [DockerFact]
    public async Task SameWord_UsesHumanContextsWithoutInternalIds_AndKeepsEveryReservation()
    {
        Clock.Reset(Instant.FromUtc(2026, 11, 2, 11, 0));
        var learner = await LearnerAsync();
        var outcomes = new[]
        {
            new ReportNotificationOutcome(Guid.NewGuid(), "kot", "applied", null, "card", "translation"),
            new ReportNotificationOutcome(Guid.NewGuid(), "kot", "applied", null, "card", "pronunciation"),
            new ReportNotificationOutcome(Guid.NewGuid(), "kot", "applied", null, "card", "lemma"),
            new ReportNotificationOutcome(Guid.NewGuid(), "kot", "applied", null, "card", "example"),
            new ReportNotificationOutcome(Guid.NewGuid(), "kot", "applied", null, "answer_dispute", null),
            new ReportNotificationOutcome(Guid.NewGuid(), "kot", "added_to_candidates", null, "missing_word", null)
        };
        var source = new OutcomeSource(outcomes);
        await using var provider = Provider(new StubTelegram());
        await using var scope = provider.CreateAsyncScope();
        var handler = new ReportOutcomesHandler(source, scope.ServiceProvider.GetRequiredService<INotificationAudienceReader>(), Clock);
        var dispatch = new NotificationDispatch
        {
            LearnerId = learner.Id,
            RecipientTelegramUserId = LearnerTelegramId,
            Kind = "report_outcomes",
            DueAt = Clock.GetCurrentInstant(),
            LocalDate = new LocalDate(2026, 11, 2),
            Status = "pending"
        };
        var result = Assert.IsType<NotificationPreparation.Deliver>(await handler.PrepareAsync(dispatch, CancellationToken.None));
        foreach (var context in new[] { "переклад", "вимова", "написання", "приклад", "варіант відповіді" })
            Assert.Contains($"kot ({context}): Виправлено.", result.Message.Text, StringComparison.Ordinal);
        Assert.Contains("kot (відсутнє слово): Додано до кандидатів.", result.Message.Text, StringComparison.Ordinal);
        Assert.Equal(6, result.Message.Text.Split('\n').Count(line => line.StartsWith("• ", StringComparison.Ordinal)));
        Assert.All(outcomes, row => Assert.DoesNotContain(row.Id.ToString(), result.Message.Text, StringComparison.Ordinal));
        Assert.DoesNotContain(dispatch.Id.ToString(), result.Message.Text, StringComparison.Ordinal);
        Assert.Equal(outcomes.Select(row => row.Id), source.ReservedIds);
    }

    [DockerFact]
    public async Task ClosedAfterNoon_WaitsForNextDay_AndEmptySummaryDoesNotSend()
    {
        Clock.Reset(Instant.FromUtc(2026, 11, 2, 11, 1));
        var learner = await LearnerAsync();
        var telegram = new StubTelegram();
        await using var provider = Provider(telegram);
        await PlanAsync(provider);
        await DispatchAsync(provider);
        Assert.Empty(telegram.Messages);
        await ClosedAsync(learner, "kot", "rejected", "duplicate", Clock.GetCurrentInstant());
        await PlanAsync(provider);
        await DispatchAsync(provider);
        Assert.Empty(telegram.Messages);
        Assert.Equal(new LocalDate(2026, 11, 3), (await Db.NotificationDispatches.SingleAsync()).LocalDate);
        Clock.Reset(Instant.FromUtc(2026, 11, 3, 11, 0));
        await DispatchAsync(provider);
        Assert.Single(telegram.Messages);
    }

    [DockerFact]
    public async Task RejectedTransient_RetainsSameReservationsAndDispatch_ThenSentDoesNotRepeat()
    {
        Clock.Reset(Instant.FromUtc(2026, 11, 2, 11, 0));
        var learner = await LearnerAsync();
        var report = await ClosedAsync(learner, "kot", "rejected", "out_of_scope");
        var telegram = new StubTelegram(attempt => attempt == 1
            ? new TelegramDeliveryException(TelegramDeliveryFailure.RejectedTransient, 45) : null);
        await using var provider = Provider(telegram);
        await PlanAsync(provider);
        await DispatchAsync(provider);
        var first = await Db.NotificationDispatches.AsNoTracking().SingleAsync();
        Assert.Equal("pending", first.Status);
        Assert.Equal(first.Id, (await Db.ContentReports.AsNoTracking().SingleAsync()).SummaryDispatchId);
        Clock.Advance(Duration.FromSeconds(44));
        await DispatchAsync(provider);
        Assert.Single(telegram.Messages);
        Clock.Advance(Duration.FromSeconds(1));
        await DispatchAsync(provider);
        var sent = await Db.NotificationDispatches.AsNoTracking().SingleAsync();
        Assert.Equal(first.Id, sent.Id);
        Assert.Equal("sent", sent.Status);
        Assert.Equal(2, sent.Attempts);
        Assert.Equal(telegram.Messages.First().Text, telegram.Messages.Last().Text);
        Assert.Contains("kot (відсутнє слово): Не підтверджено: поза межами курсу.", telegram.Messages.Last().Text, StringComparison.Ordinal);
        Assert.Equal(sent.Id, (await Db.ContentReports.AsNoTracking().SingleAsync(row => row.Id == report.Id)).SummaryDispatchId);
        Clock.Advance(Duration.FromDays(1));
        await PlanAsync(provider);
        await DispatchAsync(provider);
        Assert.Equal(2, telegram.Messages.Count);
    }

    [DockerFact]
    public async Task FailedSummary_ReleasesResults_AndNextSuccessfulDayCarriesThemOnce()
    {
        Clock.Reset(Instant.FromUtc(2026, 11, 2, 11, 0));
        var learner = await LearnerAsync();
        var report = await ClosedAsync(learner, "kot", "rejected", "already_in_dictionary");
        var telegram = new StubTelegram(attempt => attempt == 1 ? new TelegramDeliveryException(TelegramDeliveryFailure.Ambiguous) : null);
        await using var provider = Provider(telegram);
        await PlanAsync(provider);
        await DispatchAsync(provider);
        Assert.Equal("failed", (await Db.NotificationDispatches.AsNoTracking().SingleAsync()).Status);
        Assert.Null((await Db.ContentReports.AsNoTracking().SingleAsync()).SummaryDispatchId);
        Clock.Advance(Duration.FromDays(1));
        await PlanAsync(provider);
        await DispatchAsync(provider);
        Assert.Equal(2, telegram.Messages.Count);
        Assert.Contains("kot (відсутнє слово): Не підтверджено: уже є у словнику.", telegram.Messages.Last().Text, StringComparison.Ordinal);
        Assert.Equal((await Db.NotificationDispatches.AsNoTracking().SingleAsync(row => row.Status == "sent")).Id,
            (await Db.ContentReports.AsNoTracking().SingleAsync(row => row.Id == report.Id)).SummaryDispatchId);
        Clock.Advance(Duration.FromDays(1));
        await PlanAsync(provider);
        await DispatchAsync(provider);
        Assert.Equal(2, telegram.Messages.Count);
    }

    [DockerFact]
    public async Task MissedAndMessagingDisabled_ReleaseReservationsForFollowingDay()
    {
        Clock.Reset(Instant.FromUtc(2026, 11, 2, 11, 0));
        var learner = await LearnerAsync();
        var report = await ClosedAsync(learner, "kot", "rejected", "phrase_not_lemma");
        var telegram = new StubTelegram();
        await using var provider = Provider(telegram);
        await PlanAsync(provider);
        var dispatch = await Db.NotificationDispatches.AsNoTracking().SingleAsync();
        await Db.ContentReports.ExecuteUpdateAsync(update => update.SetProperty(row => row.SummaryDispatchId, dispatch.Id));
        Clock.Advance(Duration.FromMinutes(61));
        await DispatchAsync(provider);
        Assert.Equal("missed", (await Db.NotificationDispatches.AsNoTracking().SingleAsync()).Status);
        Assert.Null((await Db.ContentReports.AsNoTracking().SingleAsync()).SummaryDispatchId);
        Clock.Reset(Instant.FromUtc(2026, 11, 3, 11, 0));
        await PlanAsync(provider);
        await Db.Learners.ExecuteUpdateAsync(update => update.SetProperty(row => row.CanMessage, false));
        await DispatchAsync(provider);
        Assert.Equal("skipped", (await Db.NotificationDispatches.AsNoTracking().SingleAsync(row => row.LocalDate == new LocalDate(2026, 11, 3))).Status);
        Assert.Null((await Db.ContentReports.AsNoTracking().SingleAsync()).SummaryDispatchId);
        await Db.Learners.ExecuteUpdateAsync(update => update.SetProperty(row => row.CanMessage, true));
        Clock.Reset(Instant.FromUtc(2026, 11, 4, 11, 0));
        await PlanAsync(provider);
        await DispatchAsync(provider);
        Assert.Contains("kot (відсутнє слово): Не підтверджено: фраза, а не лема.", Assert.Single(telegram.Messages).Text, StringComparison.Ordinal);
        Assert.Equal((await Db.NotificationDispatches.AsNoTracking().SingleAsync(row => row.Status == "sent")).Id,
            (await Db.ContentReports.AsNoTracking().SingleAsync(row => row.Id == report.Id)).SummaryDispatchId);
    }

    [DockerFact]
    public async Task ParallelPlannersAndDispatchers_WithRestart_CreateAndDeliverOneDailyRow()
    {
        Clock.Reset(Instant.FromUtc(2026, 11, 2, 11, 0));
        var learner = await LearnerAsync();
        await ClosedAsync(learner, "kot", "rejected", "duplicate");
        var telegram = new StubTelegram();
        await using (var first = Provider(telegram))
            await Task.WhenAll(PlanAsync(first), PlanAsync(first));
        Assert.Single(await Db.NotificationDispatches.ToArrayAsync());
        await using (var restarted = Provider(telegram))
        {
            await Task.WhenAll(DispatchAsync(restarted), DispatchAsync(restarted));
            await PlanAsync(restarted);
            await DispatchAsync(restarted);
        }
        Assert.Single(telegram.Messages);
        Assert.Equal("sent", (await Db.NotificationDispatches.AsNoTracking().SingleAsync()).Status);
    }

    [DockerFact]
    public async Task Ac43_EditorDigest_CountsAllNewlyReceivedAndOnlyCurrentQueue_WithDurableWindowAndLink()
    {
        Clock.Reset(Instant.FromUtc(2026, 11, 2, 8, 0));
        var learner = await LearnerAsync();
        var previousDue = Instant.FromUtc(2026, 11, 1, 8, 0);
        Db.NotificationDispatches.Add(new NotificationDispatch
        {
            RecipientTelegramUserId = EditorTelegramId,
            Kind = "editor_digest",
            LocalDate = new LocalDate(2026, 11, 1),
            DueAt = previousDue,
            Status = "sent",
            SentAt = previousDue + Duration.FromMinutes(1)
        });
        await Db.SaveChangesAsync();
        await OpenAsync(learner, "in_editor_queue", previousDue);
        await OpenAsync(learner, "in_editor_queue", previousDue + Duration.FromMinutes(1));
        await OpenAsync(learner, "pending_verification", previousDue + Duration.FromMinutes(2));
        await ClosedAsync(learner, "PRIVATE", "applied", createdAt: previousDue + Duration.FromMinutes(3));
        var telegram = new StubTelegram();
        await using var provider = Provider(telegram, editors: [EditorTelegramId, EditorTelegramId]);
        await PlanAsync(provider);
        await DispatchAsync(provider);
        var message = Assert.Single(telegram.Messages);
        Assert.Equal(EditorTelegramId, message.Recipient);
        Assert.Contains("Нових повідомлень: 3.", message.Text, StringComparison.Ordinal);
        Assert.Contains("Без рішення: 2.", message.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE", message.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("R-", message.Text, StringComparison.Ordinal);
        Assert.Equal("https://trainer.example/editor", message.Markup!.InlineKeyboard.Single().Single().WebApp!.Url);
        var late = await OpenAsync(learner, "in_editor_queue", Clock.GetCurrentInstant() + Duration.FromSeconds(1));
        Clock.Advance(Duration.FromDays(1));
        await PlanAsync(provider);
        await DispatchAsync(provider);
        var next = telegram.Messages.Last(row => row.Recipient == EditorTelegramId);
        Assert.Contains("Нових повідомлень: 1.", next.Text, StringComparison.Ordinal);
        Assert.Contains("Без рішення: 3.", next.Text, StringComparison.Ordinal);
        Assert.Equal(3, await Db.NotificationDispatches.CountAsync(row => row.Kind == "editor_digest" && row.Status == "sent"));
        Assert.Null(late.ClosedAt);
    }

    [DockerFact]
    public async Task PendingVerificationOnly_DoesNotSendDigest_AndRevokedEditorDoesNotReceiveIt()
    {
        Clock.Reset(Instant.FromUtc(2026, 11, 2, 8, 0));
        var learner = await LearnerAsync();
        await OpenAsync(learner, "pending_verification", Clock.GetCurrentInstant() - Duration.FromMinutes(1));
        var telegram = new StubTelegram();
        await using var provider = Provider(telegram, editors: [EditorTelegramId]);
        await PlanAsync(provider);
        await DispatchAsync(provider);
        Assert.Empty(telegram.Messages);
        Assert.Equal("skipped", (await Db.NotificationDispatches.AsNoTracking().SingleAsync()).Status);
        Clock.Advance(Duration.FromDays(1));
        await OpenAsync(learner, "in_editor_queue", Clock.GetCurrentInstant() - Duration.FromMinutes(1));
        await PlanAsync(provider);
        await using var revoked = Provider(telegram);
        await DispatchAsync(revoked);
        Assert.Empty(telegram.Messages);
        Assert.Equal("editor_role_removed", (await Db.NotificationDispatches.AsNoTracking().SingleAsync(row => row.LocalDate == new LocalDate(2026, 11, 3))).Reason);
    }

    [SummaryDockerTheory]
    [InlineData(2026, 3, 29, 10)]
    [InlineData(2026, 10, 25, 11)]
    public async Task DstTransition_NoonUsesCurrentIanaOffset_AndOneLocalDate(int year, int month, int day, int utcHour)
    {
        Clock.Reset(Instant.FromUtc(year, month, day, utcHour - 1, 0));
        var learner = await LearnerAsync();
        await ClosedAsync(learner, "kot", "rejected", "already_correct");
        var telegram = new StubTelegram();
        await using var provider = Provider(telegram);
        await PlanAsync(provider);
        var planned = await Db.NotificationDispatches.AsNoTracking().SingleAsync();
        Assert.Equal(Instant.FromUtc(year, month, day, utcHour, 0), planned.DueAt);
        Assert.Equal(new LocalDate(year, month, day), planned.LocalDate);
        Clock.Advance(Duration.FromHours(1));
        await DispatchAsync(provider);
        await PlanAsync(provider);
        await DispatchAsync(provider);
        Assert.Single(telegram.Messages);
    }

    [DockerFact]
    public async Task SettingsTimezoneChange_RecalculatesPendingInsideSameTransaction_AndPreservesRetryDeadline()
    {
        Clock.Reset(Instant.FromUtc(2026, 11, 2, 9, 0));
        var learner = await LearnerAsync();
        await ClosedAsync(learner, "kot", "rejected", "already_correct");
        var telegram = new StubTelegram();
        await using var provider = Provider(telegram);
        await PlanAsync(provider);
        var initial = await Db.NotificationDispatches.AsNoTracking().SingleAsync();
        Assert.Equal(Instant.FromUtc(2026, 11, 2, 11, 0), initial.DueAt);
        await using var app = await ReportsTestApplication.StartAsync(_postgres.ConnectionString, Clock, services => services.AddNotifications());
        using var request = new HttpRequestMessage(HttpMethod.Patch, "/api/v1/learners/me/settings")
        {
            Content = new StringContent("{\"time_zone\":\"America/New_York\"}", Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("tma", new TelegramInitDataSigner().Sign(LearnerTelegramId, Clock.GetCurrentInstant()));
        using var response = await app.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractAssert.ResponseAsync("updateLearnerSettings", response);
        Assert.Equal(Instant.FromUtc(2026, 11, 2, 17, 0), (await Db.NotificationDispatches.AsNoTracking().SingleAsync()).DueAt);
        await Db.NotificationDispatches.ExecuteUpdateAsync(update => update.SetProperty(row => row.Attempts, (short)1)
            .SetProperty(row => row.ClaimedAt, Clock.GetCurrentInstant()).SetProperty(row => row.Reason, "telegram_rejected_transient:30"));
        await ChangeZoneInsideTransactionAsync(provider, learner.Id, "Asia/Tokyo", commit: true);
        Assert.Equal(Instant.FromUtc(2026, 11, 2, 17, 0), (await Db.NotificationDispatches.AsNoTracking().SingleAsync()).DueAt);
    }

    [DockerFact]
    public async Task SettingsRollback_RestoresBothTimezoneAndDispatch_AndReaderDoesNotLockLearner()
    {
        Clock.Reset(Instant.FromUtc(2026, 11, 2, 9, 0));
        var learner = await LearnerAsync();
        await ClosedAsync(learner, "kot", "rejected", "already_correct");
        var telegram = new StubTelegram();
        await using var provider = Provider(telegram);
        await PlanAsync(provider);
        var before = (await Db.NotificationDispatches.AsNoTracking().SingleAsync()).DueAt;
        await ChangeZoneInsideTransactionAsync(provider, learner.Id, "America/New_York", commit: false);
        Assert.Equal("Europe/Warsaw", (await Db.Learners.AsNoTracking().SingleAsync()).TimeZone);
        Assert.Equal(before, (await Db.NotificationDispatches.AsNoTracking().SingleAsync()).DueAt);
        await using var transaction = await Db.Database.BeginTransactionAsync();
        await Db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM learners WHERE id = {learner.Id} FOR UPDATE");
        await using var scope = provider.CreateAsyncScope();
        var recipient = await scope.ServiceProvider.GetRequiredService<INotificationAudienceReader>().ReadLearnerAsync(learner.Id)
            .WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(LearnerTelegramId, recipient!.TelegramUserId);
        await transaction.RollbackAsync();
    }

    [DockerFact]
    public async Task ContentReservation_RequiresClaimTransaction_AndRollbackDoesNotLoseReport()
    {
        Clock.Reset(Instant.FromUtc(2026, 11, 2, 11, 0));
        var learner = await LearnerAsync();
        var report = await ClosedAsync(learner, "kot", "rejected", "already_correct");
        await using var provider = Provider(new StubTelegram());
        await PlanAsync(provider);
        var dispatch = await Db.NotificationDispatches.AsNoTracking().SingleAsync();
        await using var scope = provider.CreateAsyncScope();
        var port = scope.ServiceProvider.GetRequiredService<IReportNotificationReader>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => port.ReadForReservationAsync(learner.Id, dispatch.Id, dispatch.DueAt));
        await using var transaction = await db.Database.BeginTransactionAsync();
        var batch = await port.ReadForReservationAsync(learner.Id, dispatch.Id, dispatch.DueAt);
        Assert.Equal(report.Id, Assert.Single(batch.Items).Id);
        await port.ReserveAsync(learner.Id, dispatch.Id, [report.Id]);
        await transaction.RollbackAsync();
        Assert.Null((await Db.ContentReports.AsNoTracking().SingleAsync()).SummaryDispatchId);
    }

    [DockerFact]
    public async Task Ac51_DuplicateAuthenticatedReport_ProducesOneResultLine()
    {
        Clock.Reset(Instant.FromUtc(2026, 11, 2, 10, 59));
        await LearnerAsync();
        await using var app = await ReportsTestApplication.StartAsync(_postgres.ConnectionString, Clock, services =>
            services.Configure<IdentityOptions>(options => options.ContentEditorTelegramUserIds = [EditorTelegramId]));
        Guid? reportId = null;
        for (var index = 0; index < 2; index++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/content-reports")
            {
                Content = new StringContent("{\"origin\":\"missing_word\",\"search_query\":\"kot\"}", Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new("tma", new TelegramInitDataSigner().Sign(LearnerTelegramId, Clock.GetCurrentInstant()));
            using var response = await app.Client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            await ContractAssert.ResponseAsync("createContentReport", response);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var id = json.RootElement.GetProperty("id").GetGuid();
            if (reportId is { } previous) Assert.Equal(previous, id);
            reportId = id;
        }
        using var decision = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/editor/content-reports/{reportId}/decision")
        {
            Content = new StringContent("{\"decision\":\"reject\",\"rejection_reason\":\"already_in_dictionary\"}", Encoding.UTF8, "application/json")
        };
        decision.Headers.Authorization = new("tma", new TelegramInitDataSigner().Sign(EditorTelegramId, Clock.GetCurrentInstant()));
        using var closed = await app.Client.SendAsync(decision);
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        var telegram = new StubTelegram();
        await using var provider = Provider(telegram);
        await PlanAsync(provider);
        Clock.Advance(Duration.FromMinutes(1));
        await DispatchAsync(provider);
        var text = Assert.Single(telegram.Messages).Text;
        Assert.Equal(1, text.Split('\n').Count(line => line.StartsWith("• ", StringComparison.Ordinal)));
        Assert.Contains("kot (відсутнє слово): Не підтверджено: уже є у словнику.", text, StringComparison.Ordinal);
        Assert.DoesNotContain(reportId!.Value.ToString(), text, StringComparison.Ordinal);
        Assert.Equal((await Db.NotificationDispatches.AsNoTracking().SingleAsync()).Id,
            (await Db.ContentReports.AsNoTracking().SingleAsync()).SummaryDispatchId);
        Assert.Equal(1, await Db.ContentReports.CountAsync());
    }

    [DockerFact]
    public async Task ClaimBeforeSettingsCommit_SeesOldTimezoneWithoutDeadlock_AndSendingKeepsOriginalDeadline()
    {
        Clock.Reset(Instant.FromUtc(2026, 11, 2, 11, 0));
        var learner = await LearnerAsync();
        await ClosedAsync(learner, "kot", "rejected", "already_correct");
        await using var provider = Provider(new StubTelegram());
        await PlanAsync(provider);
        await using var claimant = provider.CreateAsyncScope();
        var claimDb = claimant.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var claim = await claimDb.Database.BeginTransactionAsync();
        var dispatch = await claimDb.NotificationDispatches.FromSqlRaw("SELECT *, xmin FROM notification_dispatches FOR UPDATE").SingleAsync();
        dispatch.Status = "sending";
        dispatch.Attempts = 1;
        dispatch.ClaimedAt = Clock.GetCurrentInstant();
        await claimDb.SaveChangesAsync();
        var saved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var changing = ChangeZoneInsideTransactionAsync(provider, learner.Id, "America/New_York", true, saved);
        try
        {
            await saved.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var recipient = await claimant.ServiceProvider.GetRequiredService<INotificationAudienceReader>().ReadLearnerAsync(learner.Id)
                .WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal("Europe/Warsaw", recipient!.TimeZone);
        }
        finally { await claim.CommitAsync(); }
        await changing.WaitAsync(TimeSpan.FromSeconds(5));
        var stored = await Db.NotificationDispatches.AsNoTracking().SingleAsync();
        Assert.Equal("sending", stored.Status);
        Assert.Equal(Instant.FromUtc(2026, 11, 2, 11, 0), stored.DueAt);
        Assert.Equal("America/New_York", (await Db.Learners.AsNoTracking().SingleAsync()).TimeZone);
    }

    [DockerFact]
    public async Task TimezoneMoveWest_ExtendsStableDayWithoutSecondSummary_AndCarriesNewResultToNextDay()
    {
        Clock.Reset(Instant.FromUtc(2026, 11, 2, 11, 0));
        var learner = await LearnerAsync();
        await ClosedAsync(learner, "kot", "rejected", "already_correct");
        var telegram = new StubTelegram();
        await using var provider = Provider(telegram);
        await PlanAsync(provider);
        await DispatchAsync(provider);
        Assert.Single(telegram.Messages);
        await ChangeZoneInsideTransactionAsync(provider, learner.Id, "Pacific/Honolulu", true);
        var next = await ClosedAsync(learner, "pies", "rejected", "out_of_scope", Clock.GetCurrentInstant());
        Clock.Reset(Instant.FromUtc(2026, 11, 2, 22, 0));
        await PlanAsync(provider);
        await DispatchAsync(provider);
        Assert.Single(telegram.Messages);
        Assert.Single(await Db.NotificationDispatches.ToArrayAsync());
        Clock.Reset(Instant.FromUtc(2026, 11, 3, 22, 0));
        await PlanAsync(provider);
        await DispatchAsync(provider);
        Assert.Equal(2, telegram.Messages.Count);
        Assert.Contains("pies (відсутнє слово): Не підтверджено: поза межами курсу.", telegram.Messages.Last().Text, StringComparison.Ordinal);
        Assert.DoesNotContain("kot", telegram.Messages.Last().Text, StringComparison.Ordinal);
        Assert.Equal((await Db.NotificationDispatches.AsNoTracking().SingleAsync(row => row.LocalDate == new LocalDate(2026, 11, 3))).Id,
            (await Db.ContentReports.AsNoTracking().SingleAsync(row => row.Id == next.Id)).SummaryDispatchId);
        Assert.Equal(new[] { new LocalDate(2026, 11, 2), new LocalDate(2026, 11, 3) },
            await Db.NotificationDispatches.OrderBy(row => row.LocalDate).Select(row => row.LocalDate).ToArrayAsync());
    }

    [DockerFact]
    public async Task WrongRecipientOnDispatch_DoesNotExposeOrReserveAnotherLearnersResults()
    {
        Clock.Reset(Instant.FromUtc(2026, 11, 2, 11, 0));
        var learner = await LearnerAsync();
        await ClosedAsync(learner, "kot", "rejected", "already_correct");
        Db.NotificationDispatches.Add(new NotificationDispatch
        {
            LearnerId = learner.Id,
            RecipientTelegramUserId = LearnerTelegramId + 5,
            Kind = "report_outcomes",
            LocalDate = new LocalDate(2026, 11, 2),
            DueAt = Clock.GetCurrentInstant(),
            Status = "pending"
        });
        await Db.SaveChangesAsync();
        var telegram = new StubTelegram();
        await using var provider = Provider(telegram);
        await DispatchAsync(provider);
        Assert.Empty(telegram.Messages);
        Assert.Null((await Db.ContentReports.AsNoTracking().SingleAsync()).SummaryDispatchId);
        Assert.Equal("summary_recipient_unavailable", (await Db.NotificationDispatches.AsNoTracking().SingleAsync()).Reason);
    }

    [DockerFact]
    public async Task PlannerWithOldAudience_CannotOverwriteTimezoneCommittedBySettingsHook()
    {
        Clock.Reset(Instant.FromUtc(2026, 11, 2, 9, 0));
        var learner = await LearnerAsync();
        await ClosedAsync(learner, "kot", "rejected", "already_correct");
        var read = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var provider = Provider(new StubTelegram(), configure: services =>
        {
            var descriptor = services.Single(item => item.ServiceType == typeof(INotificationAudienceReader));
            services.Remove(descriptor);
            services.AddScoped<INotificationAudienceReader>(serviceProvider => new PausedAudience(
                (INotificationAudienceReader)ActivatorUtilities.CreateInstance(serviceProvider, descriptor.ImplementationType!), read, release));
        });
        var planning = PlanAsync(provider);
        Task? changing = null;
        try
        {
            await read.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            changing = ChangeZoneInsideTransactionAsync(provider, learner.Id, "America/New_York", true, started: started);
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(changing.IsCompleted);
        }
        finally { release.TrySetResult(); }
        await planning.WaitAsync(TimeSpan.FromSeconds(5));
        await changing!.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("America/New_York", (await Db.Learners.AsNoTracking().SingleAsync()).TimeZone);
        Assert.Equal(Instant.FromUtc(2026, 11, 2, 17, 0), (await Db.NotificationDispatches.AsNoTracking().SingleAsync()).DueAt);
    }

    [DockerFact]
    public async Task LargeBacklog_UsesOneBoundedPrefixPerDay_AndFailedPrefixCarriesWithoutLossOrRepeatedSuccess()
    {
        Clock.Reset(Instant.FromUtc(2026, 11, 2, 11, 0));
        var learner = await LearnerAsync();
        var reports = await BacklogAsync(learner, 60);
        var telegram = new StubTelegram(attempt => attempt == 1 ? new TelegramDeliveryException(TelegramDeliveryFailure.Ambiguous) : null);
        await using var provider = Provider(telegram);
        var previousUnassigned = reports.Length;
        for (var day = 0; day < 8 && previousUnassigned > 0; day++)
        {
            var before = telegram.Messages.Count;
            await PlanAsync(provider);
            await DispatchAsync(provider);
            Assert.Equal(before + 1, telegram.Messages.Count);
            var text = telegram.Messages.Last().Text;
            Assert.InRange(text.Length, 1, 4096);
            var included = reports.Where(row => text.Contains(BacklogLabel(row), StringComparison.Ordinal)).ToArray();
            Assert.NotEmpty(included);
            Assert.Equal(included.Length, text.Split('\n').Count(line => line.StartsWith("• ", StringComparison.Ordinal)));
            Assert.All(included, row => AssertBacklogOutcome(text, row));
            Assert.All(reports, row => Assert.DoesNotContain(row.Id.ToString(), text, StringComparison.Ordinal));
            var unassigned = await Db.ContentReports.CountAsync(row => row.SummaryDispatchId == null);
            if (day == 0)
            {
                Assert.Equal(60, unassigned);
                Assert.Equal("failed", (await Db.NotificationDispatches.AsNoTracking().SingleAsync()).Status);
            }
            else
            {
                Assert.Equal(previousUnassigned - included.Length, unassigned);
                var date = Clock.GetCurrentInstant().InZone(DateTimeZoneProviders.Tzdb[learner.TimeZone]).Date;
                var dispatch = await Db.NotificationDispatches.AsNoTracking().SingleAsync(row => row.LocalDate == date);
                Assert.Equal("sent", dispatch.Status);
                Assert.Equal(included.Select(row => row.Id).Order(),
                    (await Db.ContentReports.Where(row => row.SummaryDispatchId == dispatch.Id).Select(row => row.Id).ToArrayAsync()).Order());
            }
            var deferred = day == 0 ? reports.Length - included.Length : unassigned;
            if (deferred > 0) Assert.Contains($"Відкладено результатів: {deferred}.", text, StringComparison.Ordinal);
            previousUnassigned = unassigned;
            Clock.Advance(Duration.FromDays(1));
        }
        Assert.Equal(0, previousUnassigned);
        var successfulTexts = telegram.Messages.Skip(1).Select(item => item.Text).ToArray();
        Assert.True(successfulTexts.Length > 1);
        foreach (var report in reports)
            Assert.Equal(1, successfulTexts.Sum(text => text.Split(BacklogLabel(report), StringSplitOptions.None).Length - 1));
        Assert.Equal(60, await Db.ContentReports.CountAsync(row => row.SummaryDispatchId != null));
    }

    [DockerFact]
    public async Task OverflowRetry_IncludesAllPreviouslyReservedRows_WhenOlderFailedDispatchReleasesMoreResults()
    {
        Clock.Reset(Instant.FromUtc(2026, 11, 2, 11, 0));
        var learner = await LearnerAsync();
        var backlog = await BacklogAsync(learner, 60);
        var older = await ClosedAsync(learner, "starszy", "rejected", "duplicate", Clock.GetCurrentInstant() - Duration.FromDays(1));
        var oldDispatch = new NotificationDispatch
        {
            LearnerId = learner.Id,
            RecipientTelegramUserId = LearnerTelegramId,
            Kind = "report_outcomes",
            LocalDate = new LocalDate(2026, 11, 1),
            DueAt = Clock.GetCurrentInstant() - Duration.FromDays(1),
            Status = "sending",
            Attempts = 1,
            ClaimedAt = Clock.GetCurrentInstant()
        };
        Db.NotificationDispatches.Add(oldDispatch);
        await Db.SaveChangesAsync();
        await Db.ContentReports.Where(row => row.Id == older.Id).ExecuteUpdateAsync(update => update.SetProperty(row => row.SummaryDispatchId, oldDispatch.Id));
        var telegram = new StubTelegram(attempt => attempt == 1
            ? new TelegramDeliveryException(TelegramDeliveryFailure.RejectedTransient, 45) : null);
        await using var provider = Provider(telegram);
        await PlanAsync(provider);
        await DispatchAsync(provider);
        var current = await Db.NotificationDispatches.AsNoTracking().SingleAsync(row => row.LocalDate == new LocalDate(2026, 11, 2));
        var originalIds = await Db.ContentReports.Where(row => row.SummaryDispatchId == current.Id).Select(row => row.Id).ToArrayAsync();
        Assert.NotEmpty(originalIds);
        Assert.True(originalIds.Length < 60);
        Assert.All(backlog.Where(row => originalIds.Contains(row.Id)), row => AssertBacklogOutcome(telegram.Messages.Single().Text, row));
        Assert.Contains($"Відкладено результатів: {60 - originalIds.Length}.", telegram.Messages.Single().Text, StringComparison.Ordinal);
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync();
            await db.NotificationDispatches.Where(row => row.Id == oldDispatch.Id).ExecuteUpdateAsync(update => update.SetProperty(row => row.Status, "failed"));
            await scope.ServiceProvider.GetRequiredService<IReportSummaryDispatchService>().ReleaseAsync(oldDispatch.Id, CancellationToken.None);
            await transaction.CommitAsync();
        }
        Clock.Advance(Duration.FromSeconds(45));
        await DispatchAsync(provider);
        Assert.Equal(2, telegram.Messages.Count);
        var second = telegram.Messages.Last().Text;
        Assert.InRange(second.Length, 1, 4096);
        var included = await Db.ContentReports.Where(row => row.SummaryDispatchId == current.Id).Select(row => row.Id).ToArrayAsync();
        Assert.All(originalIds, id => Assert.Contains(id, included));
        Assert.All(backlog.Where(row => included.Contains(row.Id)), row => AssertBacklogOutcome(second, row));
        if (included.Contains(older.Id)) Assert.Contains("starszy (відсутнє слово): Не підтверджено: дублікат.", second, StringComparison.Ordinal);
        Assert.Equal(included.Length, second.Split('\n').Count(line => line.StartsWith("• ", StringComparison.Ordinal)));
        Assert.All(backlog.Append(older), row => Assert.DoesNotContain(row.Id.ToString(), second, StringComparison.Ordinal));
        Assert.Equal("sent", (await Db.NotificationDispatches.AsNoTracking().SingleAsync(row => row.Id == current.Id)).Status);
        var remaining = await Db.ContentReports.CountAsync(row => row.SummaryDispatchId == null);
        Assert.Equal(61 - included.Length, remaining);
        Assert.Contains($"Відкладено результатів: {remaining}.", second, StringComparison.Ordinal);
    }

    [DockerFact]
    public async Task QuartzRegistration_UsesThirtySecondNonConcurrentPlanner()
    {
        await using var provider = Provider(new StubTelegram());
        await using var logging = new QuartzTestLoggingScope(provider);
        var scheduler = await logging.GetSchedulerAsync();
        var detail = await scheduler.GetJobDetail(new JobKey("SummaryPlanner"));
        Assert.Equal(typeof(SummaryPlannerJob), detail!.JobType);
        Assert.True(detail.ConcurrentExecutionDisallowed);
        var trigger = Assert.IsAssignableFrom<ISimpleTrigger>(await scheduler.GetTrigger(new TriggerKey("SummaryPlanner-trigger")));
        Assert.Equal(TimeSpan.FromSeconds(30), trigger.RepeatInterval);
    }

    [DockerFact]
    public async Task QuartzRegistration_AfterDisposedHost_KeepsJobsAcrossSeveralProviders()
    {
        await using (var host = await ReportsTestApplication.StartAsync(_postgres.ConnectionString, Clock))
        {
            await using var logging = new QuartzTestLoggingScope(host.Services);
            var scheduler = await logging.GetSchedulerAsync();
            Assert.Equal(typeof(ConstantLearning.Features.Content.Verification.ContentVerificationJob),
                (await scheduler.GetJobDetail(new JobKey("ContentVerification")))!.JobType);
        }
        for (var index = 0; index < 3; index++)
        {
            await using var provider = Provider(new StubTelegram());
            await using var logging = new QuartzTestLoggingScope(provider);
            var scheduler = await logging.GetSchedulerAsync();
            var detail = await scheduler.GetJobDetail(new JobKey("SummaryPlanner"));
            Assert.Equal(typeof(SummaryPlannerJob), detail!.JobType);
            Assert.True(detail.ConcurrentExecutionDisallowed);
            var trigger = Assert.IsAssignableFrom<ISimpleTrigger>(await scheduler.GetTrigger(new TriggerKey("SummaryPlanner-trigger")));
            Assert.Equal(TimeSpan.FromSeconds(30), trigger.RepeatInterval);
        }
    }

    private async Task<Learner> LearnerAsync(string zone = "Europe/Warsaw", long telegramId = LearnerTelegramId)
    {
        var learner = new Learner
        {
            TelegramUserId = telegramId,
            FirstName = "PRIVATE Name",
            ReporterAlias = "R-" + Guid.NewGuid().ToString("N")[..12],
            TimeZone = zone,
            TimeZoneConfirmed = true,
            DailyNewWordsLimit = 5,
            CanMessage = true,
            CreatedAt = Clock.GetCurrentInstant() - Duration.FromDays(10),
            OnboardedAt = Clock.GetCurrentInstant() - Duration.FromDays(10)
        };
        Db.Learners.Add(learner);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        return learner;
    }

    private async Task<ContentReport> ClosedAsync(Learner learner, string label, string outcome, string? reason = null,
        Instant? closedAt = null, Instant? createdAt = null)
    {
        var report = new ContentReport
        {
            LearnerId = learner.Id,
            Origin = "missing_word",
            SearchQuery = label,
            Comment = "PRIVATE comment",
            Status = "closed",
            Outcome = outcome,
            RejectionReason = reason,
            ClosedAt = closedAt ?? Clock.GetCurrentInstant() - Duration.FromMinutes(1),
            CreatedAt = createdAt ?? Clock.GetCurrentInstant() - Duration.FromHours(2)
        };
        Db.ContentReports.Add(report);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        return report;
    }

    private async Task<ContentReport> OpenAsync(Learner learner, string status, Instant createdAt)
    {
        var report = new ContentReport
        {
            LearnerId = learner.Id,
            Origin = "missing_word",
            SearchQuery = "PRIVATE query",
            Status = status,
            CreatedAt = createdAt
        };
        Db.ContentReports.Add(report);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        return report;
    }

    private async Task<ContentReport[]> BacklogAsync(Learner learner, int count)
    {
        var reports = Enumerable.Range(0, count).Select(index => new ContentReport
        {
            LearnerId = learner.Id,
            Origin = "missing_word",
            SearchQuery = $"słowo{index:D3}-" + new string('ż', 170),
            Status = "closed",
            Outcome = "rejected",
            RejectionReason = "source_not_confirming",
            CreatedAt = Clock.GetCurrentInstant() - Duration.FromDays(2 + index / 20),
            ClosedAt = Clock.GetCurrentInstant() - Duration.FromMinutes(1)
        }).ToArray();
        Db.ContentReports.AddRange(reports);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        return reports;
    }

    private static string BacklogLabel(ContentReport report) => report.SearchQuery!.Split('-', 2)[0];

    private static void AssertBacklogOutcome(string text, ContentReport report)
    {
        var line = Assert.Single(text.Split('\n'), item => item.Contains(BacklogLabel(report), StringComparison.Ordinal));
        Assert.EndsWith("(відсутнє слово): Не підтверджено: джерело не підтверджує.", line, StringComparison.Ordinal);
    }

    private ServiceProvider Provider(StubTelegram telegram, long[]? editors = null, Action<IServiceCollection>? configure = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = _postgres.ConnectionString,
            ["Telegram:BotToken"] = TelegramInitDataSigner.TestBotToken,
            ["Telegram:MiniAppUrl"] = "https://trainer.example/"
        };
        foreach (var (id, index) in (editors ?? []).Select((id, index) => (id, index)))
            settings[$"Identity:ContentEditorTelegramUserIds:{index}"] = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection().AddLogging().AddSingleton<IClock>(Clock).AddPlatform(configuration)
            .AddIdentity(configuration).AddLearning().AddContent().AddNotifications().AddSingleton<ITelegramClient>(telegram);
        configure?.Invoke(services);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private async Task ChangeZoneInsideTransactionAsync(ServiceProvider provider, Guid learnerId, string zone, bool commit,
        TaskCompletionSource? saved = null, TaskCompletionSource? started = null)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var days = scope.ServiceProvider.GetRequiredService<ILearnerDayStateService>();
        started?.TrySetResult();
        var current = await days.GetForUpdateAsync(learnerId);
        var updated = days.ChangeTimeZone(current!, zone);
        await db.SaveChangesAsync();
        saved?.TrySetResult();
        await scope.ServiceProvider.GetServices<ILearnerSettingsChangedHandler>().OfType<SummarySettingsChangedHandler>().Single().HandleAsync(
            new LearnerSettingsChanged(learnerId, Clock.GetCurrentInstant(), updated.Day), CancellationToken.None);
        if (commit) await transaction.CommitAsync();
        else await transaction.RollbackAsync();
    }

    private static async Task PlanAsync(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<SummaryPlanner>().TickAsync();
    }
    private static async Task DispatchAsync(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<NotificationDispatcher>().TickAsync();
    }
    private static void AssertEvent(NotificationDispatch dispatch) => ContractAssert.EventRow("notification_dispatches", JsonSerializer.Serialize(new
    {
        id = dispatch.Id,
        recipient_telegram_user_id = dispatch.RecipientTelegramUserId,
        learner_id = dispatch.LearnerId,
        kind = dispatch.Kind,
        local_date = LocalDatePattern.Iso.Format(dispatch.LocalDate),
        due_at = InstantPattern.ExtendedIso.Format(dispatch.DueAt),
        status = dispatch.Status
    }));

    private sealed record SentMessage(long Recipient, string Text, ParseMode ParseMode, InlineKeyboardMarkup? Markup);
    private sealed class OutcomeSource(IReadOnlyList<ReportNotificationOutcome> outcomes) : IReportNotificationReader
    {
        public IReadOnlyCollection<Guid> ReservedIds { get; private set; } = [];
        public Task<ReportNotificationBatch> ReadForReservationAsync(Guid learnerId, Guid dispatchId, Instant through,
            CancellationToken cancellationToken = default) => Task.FromResult(new ReportNotificationBatch(outcomes, outcomes.Count));
        public Task ReserveAsync(Guid learnerId, Guid dispatchId, IReadOnlyCollection<Guid> reportIds,
            CancellationToken cancellationToken = default)
        {
            ReservedIds = reportIds;
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<UnsummarizedReportRecipient>> ReadRecipientsAsync(Guid? after, int limit,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EditorReportCounts> ReadEditorCountsAsync(Instant? after, Instant through,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class PausedAudience(INotificationAudienceReader inner, TaskCompletionSource read,
        TaskCompletionSource release) : INotificationAudienceReader
    {
        public Task<IReadOnlyList<LearnerNotificationRecipient>> ReadLearnersAsync(IReadOnlyCollection<Guid> learnerIds,
            CancellationToken cancellationToken = default) => inner.ReadLearnersAsync(learnerIds, cancellationToken);
        public async Task<LearnerNotificationRecipient?> ReadLearnerAsync(Guid learnerId, CancellationToken cancellationToken = default)
        {
            var result = await inner.ReadLearnerAsync(learnerId, cancellationToken);
            read.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
            return result;
        }
        public Task<LearnerNotificationRecipient?> ReadForPlanningAsync(Guid learnerId, CancellationToken cancellationToken = default) =>
            inner.ReadForPlanningAsync(learnerId, cancellationToken);
        public IReadOnlyList<long> ReadEditorTelegramIds() => inner.ReadEditorTelegramIds();
    }
    private sealed class StubTelegram(Func<int, TelegramDeliveryException?>? failure = null) : ITelegramClient
    {
        private int _attempts;
        public ConcurrentQueue<SentMessage> Messages { get; } = new();
        public Task SendMessageAsync(long chatId, string text, ParseMode parseMode = ParseMode.None,
            InlineKeyboardMarkup? replyMarkup = null, CancellationToken cancellationToken = default)
        {
            Messages.Enqueue(new(chatId, text, parseMode, replyMarkup));
            if (failure?.Invoke(Interlocked.Increment(ref _attempts)) is { } exception) throw exception;
            return Task.CompletedTask;
        }
        public Task ConfigureWebhookAsync(string url, string secretToken, IReadOnlyCollection<UpdateType> allowedUpdates,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}

public sealed class SummaryDockerTheoryAttribute : TheoryAttribute
{
    public SummaryDockerTheoryAttribute() => Skip = DockerAvailability.GetSkipReason(DockerAvailability.IsCi, DockerAvailability.UnavailableReason);
}
