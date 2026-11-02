using System.Text.Json.Nodes;
using ConstantLearning.Features.Learning.Data;
using ConstantLearning.Tests.Content.Data;
using ConstantLearning.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using Npgsql;

namespace ConstantLearning.Tests.Learning.Data;

public sealed class AnswerNeutralizationMigrationTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    [DockerFact]
    public async Task Neutralization_RejectsSecondRecordForSameAnswer()
    {
        var (answer, reportId) = await CreateDisputeAsync();
        Db.AnswerNeutralizations.Add(new AnswerNeutralization
        {
            AnswerEventId = answer.Id,
            ContentReportId = reportId
        });
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        Db.AnswerNeutralizations.Add(new AnswerNeutralization
        {
            AnswerEventId = answer.Id,
            ContentReportId = reportId
        });

        var action = () => Db.SaveChangesAsync();
        var exception = await action.Should().ThrowAsync<DbUpdateException>();
        var postgres = exception.Which.InnerException.Should().BeOfType<PostgresException>().Which;
        postgres.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
        postgres.ConstraintName.Should().Be("ux_answer_neutralizations_answer_event");
    }

    [DockerFact]
    public async Task Neutralization_PreservesAnswerSnapshotAndCascadesWhenLearnerIsDeleted()
    {
        var (answer, reportId) = await CreateDisputeAsync();
        var neutralization = new AnswerNeutralization
        {
            AnswerEventId = answer.Id,
            ContentReportId = reportId
        };
        Db.AnswerNeutralizations.Add(neutralization);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();

        var stored = await Db.AnswerNeutralizations.SingleAsync();
        stored.Should().BeEquivalentTo(neutralization);
        stored.CreatedAt.Should().NotBe(default(Instant));
        stored.Id.Version.Should().Be(7);
        var storedAnswer = await Db.AnswerEvents.SingleAsync();
        storedAnswer.Should().BeEquivalentTo(answer, options => options.Excluding(value => value.StateBefore));
        JsonNode.DeepEquals(JsonNode.Parse(storedAnswer.StateBefore), JsonNode.Parse(answer.StateBefore))
            .Should().BeTrue("нейтралізація доповнює журнал, а не переписує початкову відповідь");

        var deleteReport = () => Db.ContentReports.ExecuteDeleteAsync();
        var protectedReport = await deleteReport.Should().ThrowAsync<PostgresException>();
        protectedReport.Which.ConstraintName.Should().Be("answer_neutralizations_content_report_id_fkey");

        await Db.Learners.ExecuteDeleteAsync();
        (await Db.AnswerEvents.CountAsync()).Should().Be(0);
        (await Db.ContentReports.CountAsync()).Should().Be(0);
        (await Db.AnswerNeutralizations.CountAsync()).Should().Be(0);
        (await Db.DictionaryEntries.CountAsync()).Should().Be(1);
    }

    private async Task<(AnswerEvent Answer, Guid ReportId)> CreateDisputeAsync()
    {
        var root = await ReportsPublicationTestData.CreateRootAsync(Db, Clock);
        var answer = ReportsPublicationTestData.NewAnswer(root, Clock.GetCurrentInstant());
        Db.AnswerEvents.Add(answer);
        await Db.SaveChangesAsync();
        var report = ReportsPublicationTestData.NewReport(root);
        report.Origin = "answer_dispute";
        report.AnswerEventId = answer.Id;
        Db.ContentReports.Add(report);
        await Db.SaveChangesAsync();
        return (answer, report.Id);
    }
}
