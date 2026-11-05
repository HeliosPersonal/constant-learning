using ConstantLearning.Features.Content.Data;
using ConstantLearning.Features.Notifications.Data;
using ConstantLearning.Tests.Infrastructure;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ConstantLearning.Tests.Content.Data;

public sealed class ContentReportDedupMigrationTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    [DockerFact]
    public async Task OpenDedup_RejectsSameNonNullKeyForSameLearner()
    {
        var root = await ReportsPublicationTestData.CreateRootAsync(Db, Clock);
        var report = ReportsPublicationTestData.NewReport(root);
        report.DedupKey = new string('a', 64);
        Db.ContentReports.Add(report);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        var duplicate = ReportsPublicationTestData.NewReport(root);
        duplicate.DedupKey = report.DedupKey;
        Db.ContentReports.Add(duplicate);

        await AssertUniqueViolationAsync("ux_content_reports_open_dedup");
    }

    [DockerFact]
    public async Task OpenDedup_AllowsNullKeysClosedHistoryAndDifferentLearners()
    {
        var root = await ReportsPublicationTestData.CreateRootAsync(Db, Clock);
        var otherRoot = await ReportsPublicationTestData.CreateRootAsync(Db, Clock);
        var open = ReportsPublicationTestData.NewReport(root);
        open.DedupKey = new string('a', 64);
        var otherLearner = ReportsPublicationTestData.NewReport(otherRoot);
        otherLearner.DedupKey = open.DedupKey;
        var closed = ReportsPublicationTestData.NewReport(root);
        closed.DedupKey = open.DedupKey;
        closed.Status = "closed";
        closed.ClosedAt = Clock.GetCurrentInstant();
        var otherClosed = ReportsPublicationTestData.NewReport(root);
        otherClosed.DedupKey = open.DedupKey;
        otherClosed.Status = "closed";
        otherClosed.ClosedAt = Clock.GetCurrentInstant();
        Db.ContentReports.AddRange(open, otherLearner, closed, otherClosed,
            ReportsPublicationTestData.NewReport(root), ReportsPublicationTestData.NewReport(root));
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();

        (await Db.ContentReports.CountAsync()).Should().Be(6);
        (await Db.ContentReports.CountAsync(report => report.DedupKey == null)).Should().Be(2);
        (await Db.ContentReports.CountAsync(report => report.ClosedAt != null)).Should().Be(2);
    }

    [DockerFact]
    public async Task AnswerEventUnique_ProtectsClosedAndOpenReportsForSameAnswer()
    {
        var root = await ReportsPublicationTestData.CreateRootAsync(Db, Clock);
        var answer = ReportsPublicationTestData.NewAnswer(root, Clock.GetCurrentInstant());
        Db.AnswerEvents.Add(answer);
        await Db.SaveChangesAsync();
        var closed = ReportsPublicationTestData.NewReport(root);
        closed.Origin = "answer_dispute";
        closed.AnswerEventId = answer.Id;
        closed.Status = "closed";
        closed.Outcome = "rejected";
        closed.ClosedAt = Clock.GetCurrentInstant();
        Db.ContentReports.Add(closed);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        var duplicate = ReportsPublicationTestData.NewReport(root);
        duplicate.Origin = "answer_dispute";
        duplicate.AnswerEventId = answer.Id;
        Db.ContentReports.Add(duplicate);

        await AssertUniqueViolationAsync("ux_content_reports_answer_event_id");
    }

    [DockerFact]
    public async Task SummaryDispatch_ForeignKeyProtectsClaimAndClearingItRestoresUnsentReports()
    {
        var root = await ReportsPublicationTestData.CreateRootAsync(Db, Clock);
        var dispatch = new NotificationDispatch
        {
            RecipientTelegramUserId = root.Learner.TelegramUserId,
            LearnerId = root.Learner.Id,
            Kind = "report_outcomes",
            LocalDate = Clock.GetCurrentInstant().InUtc().Date,
            DueAt = Clock.GetCurrentInstant(),
            Status = "sending"
        };
        Db.NotificationDispatches.Add(dispatch);
        await Db.SaveChangesAsync();
        var claimed = ReportsPublicationTestData.NewReport(root);
        claimed.Status = "closed";
        claimed.ClosedAt = Clock.GetCurrentInstant();
        claimed.SummaryDispatchId = dispatch.Id;
        var unsent = ReportsPublicationTestData.NewReport(root);
        unsent.Status = "closed";
        unsent.ClosedAt = Clock.GetCurrentInstant();
        Db.ContentReports.AddRange(claimed, unsent);
        await Db.SaveChangesAsync();

        (await Db.ContentReports.CountAsync(report => report.ClosedAt != null && report.SummaryDispatchId == null))
            .Should().Be(1);
        var deleteDispatch = () => Db.NotificationDispatches.ExecuteDeleteAsync();
        var exception = await deleteDispatch.Should().ThrowAsync<PostgresException>();
        exception.Which.ConstraintName.Should().Be("content_reports_summary_dispatch_id_fkey");

        dispatch.Status = "failed";
        claimed.SummaryDispatchId = null;
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        (await Db.ContentReports.CountAsync(report => report.ClosedAt != null && report.SummaryDispatchId == null))
            .Should().Be(2);
        await Db.NotificationDispatches.ExecuteDeleteAsync();
        (await Db.ContentReports.CountAsync()).Should().Be(2);
    }

    private async Task AssertUniqueViolationAsync(string constraint)
    {
        var action = () => Db.SaveChangesAsync();
        var exception = await action.Should().ThrowAsync<DbUpdateException>();
        var postgres = exception.Which.InnerException.Should().BeOfType<PostgresException>().Which;
        postgres.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
        postgres.ConstraintName.Should().Be(constraint);
    }
}
