using ConstantLearning.Features.Notifications.Data;
using ConstantLearning.Platform.Persistence;
using ConstantLearning.Tests.Content.Data;
using ConstantLearning.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Npgsql;

namespace ConstantLearning.Tests.Notifications.Data;

public sealed class NotificationDispatchMigrationTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;

    [DockerFact]
    public async Task Dispatch_RoundTripsDatesRetryFieldsAndMatchesEventPayloadContract()
    {
        var dispatch = await CreateDispatchAsync();
        dispatch.ClaimedAt.Should().BeNull();
        dispatch.SentAt.Should().BeNull();
        dispatch.Reason.Should().BeNull();
        await AssertPayloadAsync(dispatch.Id);
        dispatch.Status = "sent";
        dispatch.Attempts = 2;
        dispatch.ClaimedAt = dispatch.DueAt;
        dispatch.SentAt = dispatch.DueAt + Duration.FromSeconds(30);
        dispatch.Reason = "Тестова доставка";
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();

        var stored = await Db.NotificationDispatches.SingleAsync();
        stored.Should().BeEquivalentTo(dispatch);
        stored.CreatedAt.Should().NotBe(default(Instant));
        stored.Version.Should().NotBe(0);
        stored.Id.Version.Should().Be(7);
        await AssertPayloadAsync(dispatch.Id);
    }

    [DockerFact]
    public async Task Dispatch_IsUniquePerRecipientKindAndDate()
    {
        var dispatch = await CreateDispatchAsync();
        var otherKind = NewDispatch(dispatch.RecipientTelegramUserId, dispatch.LearnerId);
        otherKind.Kind = "report_outcomes";
        var nextDay = NewDispatch(dispatch.RecipientTelegramUserId, dispatch.LearnerId);
        nextDay.LocalDate = dispatch.LocalDate.PlusDays(1);
        nextDay.DueAt = dispatch.DueAt + Duration.FromDays(1);
        Db.NotificationDispatches.AddRange(otherKind, nextDay);
        await Db.SaveChangesAsync();
        (await Db.NotificationDispatches.CountAsync()).Should().Be(3);
        Db.ChangeTracker.Clear();
        Db.NotificationDispatches.Add(NewDispatch(dispatch.RecipientTelegramUserId, null));

        var action = () => Db.SaveChangesAsync();
        var exception = await action.Should().ThrowAsync<DbUpdateException>();
        var postgres = exception.Which.InnerException.Should().BeOfType<PostgresException>().Which;
        postgres.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
        postgres.ConstraintName.Should().Be("ux_notification_dispatches_recipient_kind_date");
    }

    [DockerFact]
    public async Task Dispatch_AllowsNullLearnerAndCascadesOnlyOwnedRows()
    {
        var owned = await CreateDispatchAsync();
        var editor = NewDispatch(owned.RecipientTelegramUserId, null);
        editor.Kind = "editor_digest";
        Db.NotificationDispatches.Add(editor);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();

        await Db.Learners.Where(learner => learner.Id == owned.LearnerId).ExecuteDeleteAsync();
        var remaining = await Db.NotificationDispatches.SingleAsync();
        remaining.Id.Should().Be(editor.Id);
        remaining.LearnerId.Should().BeNull();
        await AssertPayloadAsync(editor.Id);
    }

    [DockerFact]
    public async Task Dispatch_XminRejectsStaleClaim()
    {
        var dispatch = await CreateDispatchAsync();
        await using (var provider = _postgres.CreateServices(Clock))
        {
            await using var scope = provider.CreateAsyncScope();
            var otherDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var current = await otherDb.NotificationDispatches.SingleAsync();
            current.Status = "sending";
            current.ClaimedAt = Clock.GetCurrentInstant();
            await otherDb.SaveChangesAsync();
        }

        dispatch.Status = "skipped";
        var action = () => Db.SaveChangesAsync();
        await action.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    private async Task<NotificationDispatch> CreateDispatchAsync()
    {
        var root = await ReportsPublicationTestData.CreateRootAsync(Db, Clock);
        var dispatch = NewDispatch(root.Learner.TelegramUserId, root.Learner.Id);
        Db.NotificationDispatches.Add(dispatch);
        await Db.SaveChangesAsync();
        return dispatch;
    }

    private NotificationDispatch NewDispatch(long recipient, Guid? learnerId) => new()
    {
        RecipientTelegramUserId = recipient,
        LearnerId = learnerId,
        Kind = "reminder",
        LocalDate = Clock.GetCurrentInstant().InUtc().Date,
        DueAt = Clock.GetCurrentInstant() + Duration.FromHours(1),
        Status = "pending"
    };

    private async Task AssertPayloadAsync(Guid id)
    {
        var json = await Db.Database.SqlQueryRaw<string>("""
            SELECT json_build_object('id', id, 'recipient_telegram_user_id', recipient_telegram_user_id,
                'learner_id', learner_id, 'kind', kind, 'local_date', local_date, 'due_at', due_at,
                'status', status)::text AS "Value"
            FROM notification_dispatches WHERE id = {0}
            """, id).SingleAsync();
        ContractAssert.EventRow("notification_dispatches", json);
    }
}
