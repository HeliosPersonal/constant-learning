using ConstantLearning.Features.Content.Data;
using ConstantLearning.Platform.Persistence;
using ConstantLearning.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Npgsql;

namespace ConstantLearning.Tests.Content.Data;

public sealed class ReportsPublicationMigrationTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;

    [DockerFact]
    public async Task Migrations_ApplyInOrderAndRollbackRelationsAndConstraints()
    {
        var databaseName = "reports_migrations_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(_postgres.ConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin))
        {
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            var connectionString = new NpgsqlConnectionStringBuilder(_postgres.ConnectionString)
            {
                Database = databaseName,
                Pooling = false
            }.ConnectionString;
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connectionString, npgsql => npgsql.UseNodaTime())
                .UseSnakeCaseNamingConvention()
                .Options;
            await using var db = new AppDbContext(options);
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("CreateAnswerEvents");
            var baseline = await ReadSchemaObjectsAsync(db);
            await migrator.MigrateAsync("CreatePublicationLog");
            await ReportsPublicationSchemaAssertions.AssertAsync(db);
            (await db.Database.GetAppliedMigrationsAsync()).TakeLast(5)
                .Select(migration => migration[15..]).Should().Equal(
                    "CreateContentReports", "AddContentReportForeignKeyToEntryVersions",
                    "CreateAnswerNeutralizations", "CreateValidationEvidence", "CreatePublicationLog");

            await migrator.MigrateAsync("CreateAnswerEvents");
            (await ReadSchemaObjectsAsync(db)).Should().BeEquivalentTo(baseline,
                "відкат має прибрати нові таблиці, індекси й FK з наявної entry_versions");
            await migrator.MigrateAsync(Migration.InitialDatabase);
            (await ReadSchemaObjectsAsync(db)).Should().BeEmpty();
            (await db.Database.GetAppliedMigrationsAsync()).Should().BeEmpty();
            await migrator.MigrateAsync("CreatePublicationLog");
            await ReportsPublicationSchemaAssertions.AssertAsync(db);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    [DockerFact]
    public async Task ReportsAndPublication_RoundTripSourceResultCycleEvidenceAndHistory()
    {
        var root = await ReportsPublicationTestData.CreateRootAsync(Db, Clock);
        var answer = ReportsPublicationTestData.NewAnswer(root, Clock.GetCurrentInstant());
        Db.AnswerEvents.Add(answer);
        await Db.SaveChangesAsync();
        var report = ReportsPublicationTestData.NewReport(root);
        report.Origin = "answer_dispute";
        report.AnswerEventId = answer.Id;
        report.Field = "translation";
        report.ProposedValue = "жабка";
        report.Comment = "Тестове уточнення перекладу";
        Db.ContentReports.Add(report);
        await Db.SaveChangesAsync();

        // Звіт зберігається перед новою версією; вказівник результату додається останнім.
        var revised = ReportsPublicationTestData.NewVersion(root.Entry);
        revised.BasedOnVersionId = root.Version.Id;
        revised.ContentReportId = report.Id;
        revised.CreatedByKind = "auto_verification";
        revised.ContentHash = new string('b', 64);
        Db.EntryVersions.Add(revised);
        await Db.SaveChangesAsync();
        report.ResultingVersionId = revised.Id;
        report.Status = "closed";
        report.Outcome = "applied";
        report.ClosedAt = Clock.GetCurrentInstant() + Duration.FromMinutes(2);
        var evidence = ReportsPublicationTestData.NewEvidence(revised.Id, Clock.GetCurrentInstant());
        evidence.RevisionId = "test-revision";
        evidence.Attribution = "Тестова атрибуція";
        var publication = new PublicationLog
        {
            EntryId = root.Entry.Id,
            FromVersionId = root.Version.Id,
            ToVersionId = revised.Id,
            Action = "publish",
            ActorKind = "auto_verification",
            ActorLearnerId = root.Learner.Id
        };
        Db.AddRange(evidence, publication);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();

        var storedReport = await Db.ContentReports.SingleAsync();
        storedReport.Should().BeEquivalentTo(report);
        storedReport.CreatedAt.Should().NotBe(default(Instant));
        storedReport.Version.Should().NotBe(0);
        var storedVersion = await Db.EntryVersions.SingleAsync(value => value.Id == revised.Id);
        storedVersion.ContentReportId.Should().Be(report.Id);
        storedReport.ResultingVersionId.Should().Be(storedVersion.Id);
        var storedEvidence = await Db.ValidationEvidence.SingleAsync();
        storedEvidence.Should().BeEquivalentTo(evidence);
        storedEvidence.CreatedAt.Should().NotBe(default(Instant));
        var storedPublication = await Db.PublicationLog.SingleAsync();
        storedPublication.Should().BeEquivalentTo(publication);
        storedPublication.CreatedAt.Should().NotBe(default(Instant));
        new[] { report.Id, revised.Id, evidence.Id, publication.Id }
            .Should().OnlyContain(id => id.Version == 7);
    }

    [DockerFact]
    public async Task MissingWordReport_AllowsNullableReferencesAndStoresRejection()
    {
        var root = await ReportsPublicationTestData.CreateRootAsync(Db, Clock);
        var report = new ContentReport
        {
            LearnerId = root.Learner.Id,
            Origin = "missing_word",
            SearchQuery = "тестове слово",
            Status = "pending_verification"
        };
        Db.ContentReports.Add(report);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        var stored = await Db.ContentReports.SingleAsync();
        stored.EntryId.Should().BeNull();
        stored.VersionId.Should().BeNull();
        stored.AnswerEventId.Should().BeNull();
        stored.ResultingVersionId.Should().BeNull();
        stored.Field.Should().BeNull();
        stored.ProposedValue.Should().BeNull();
        stored.Comment.Should().BeNull();
        stored.Outcome.Should().BeNull();
        stored.RejectionReason.Should().BeNull();
        stored.ClosedAt.Should().BeNull();
        stored.Status = "closed";
        stored.Outcome = "rejected";
        stored.RejectionReason = "insufficient_evidence";
        stored.ClosedAt = Clock.GetCurrentInstant();
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();

        var closed = await Db.ContentReports.SingleAsync();
        closed.SearchQuery.Should().Be(report.SearchQuery);
        closed.Outcome.Should().Be("rejected");
        closed.RejectionReason.Should().Be("insufficient_evidence");
        closed.ClosedAt.Should().Be(Clock.GetCurrentInstant());
    }

    [DockerFact]
    public async Task EntryVersion_RejectsOrphanContentReportId()
    {
        var root = await ReportsPublicationTestData.CreateRootAsync(Db, Clock);
        var orphan = ReportsPublicationTestData.NewVersion(root.Entry);
        orphan.ContentReportId = Guid.CreateVersion7();
        Db.EntryVersions.Add(orphan);

        var action = () => Db.SaveChangesAsync();
        var exception = await action.Should().ThrowAsync<DbUpdateException>();
        var postgres = exception.Which.InnerException.Should().BeOfType<PostgresException>().Which;
        postgres.SqlState.Should().Be(PostgresErrorCodes.ForeignKeyViolation);
        postgres.ConstraintName.Should().Be("fk_entry_versions_content_report");
    }

    [DockerFact]
    public async Task ContentReport_ForeignKeyProtectsVersionProvenance()
    {
        var root = await ReportsPublicationTestData.CreateRootAsync(Db, Clock);
        var report = ReportsPublicationTestData.NewReport(root);
        Db.ContentReports.Add(report);
        await Db.SaveChangesAsync();
        var revised = ReportsPublicationTestData.NewVersion(root.Entry);
        revised.ContentReportId = report.Id;
        Db.EntryVersions.Add(revised);
        await Db.SaveChangesAsync();

        var action = () => Db.ContentReports.ExecuteDeleteAsync();
        var exception = await action.Should().ThrowAsync<PostgresException>();
        exception.Which.SqlState.Should().Be(PostgresErrorCodes.ForeignKeyViolation);
        exception.Which.ConstraintName.Should().Be("fk_entry_versions_content_report");
    }

    [DockerFact]
    public async Task ContentReport_XminRejectsStaleClosure()
    {
        var root = await ReportsPublicationTestData.CreateRootAsync(Db, Clock);
        var report = ReportsPublicationTestData.NewReport(root);
        Db.ContentReports.Add(report);
        await Db.SaveChangesAsync();
        await using (var provider = _postgres.CreateServices(Clock))
        {
            await using var scope = provider.CreateAsyncScope();
            var otherDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var current = await otherDb.ContentReports.SingleAsync();
            current.Status = "closed";
            current.Outcome = "rejected";
            current.ClosedAt = Clock.GetCurrentInstant();
            await otherDb.SaveChangesAsync();
        }

        report.Status = "in_editor_queue";
        var action = () => Db.SaveChangesAsync();
        await action.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [DockerFact]
    public async Task ValidationEvidence_AllowsNullableAttributionAndCascadesWithVersion()
    {
        var root = await ReportsPublicationTestData.CreateRootAsync(Db, Clock);
        var evidence = ReportsPublicationTestData.NewEvidence(root.Version.Id, Clock.GetCurrentInstant());
        Db.ValidationEvidence.Add(evidence);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        var stored = await Db.ValidationEvidence.SingleAsync();
        stored.RevisionId.Should().BeNull();
        stored.Attribution.Should().BeNull();

        await Db.EntryVersions.ExecuteDeleteAsync();
        (await Db.ValidationEvidence.CountAsync()).Should().Be(0);
        (await Db.DictionaryEntries.CountAsync()).Should().Be(1);
    }

    [DockerFact]
    public async Task PublicationLog_AllowsNullVersionAndActorAndCascadesWithEntry()
    {
        var root = await ReportsPublicationTestData.CreateRootAsync(Db, Clock);
        Db.PublicationLog.Add(new PublicationLog
        {
            EntryId = root.Entry.Id,
            Action = "unpublish",
            ActorKind = "import"
        });
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        var stored = await Db.PublicationLog.SingleAsync();
        stored.FromVersionId.Should().BeNull();
        stored.ToVersionId.Should().BeNull();
        stored.ActorLearnerId.Should().BeNull();

        await Db.DictionaryEntries.ExecuteDeleteAsync();
        (await Db.PublicationLog.CountAsync()).Should().Be(0);
        (await Db.EntryVersions.CountAsync()).Should().Be(0);
    }

    [DockerFact]
    public async Task PublicationLog_ForeignKeysProtectVersionAndActor()
    {
        var root = await ReportsPublicationTestData.CreateRootAsync(Db, Clock);
        Db.PublicationLog.Add(new PublicationLog
        {
            EntryId = root.Entry.Id,
            ToVersionId = root.Version.Id,
            Action = "publish",
            ActorKind = "editor",
            ActorLearnerId = root.Learner.Id
        });
        await Db.SaveChangesAsync();

        var deleteVersion = () => Db.EntryVersions.ExecuteDeleteAsync();
        var versionException = await deleteVersion.Should().ThrowAsync<PostgresException>();
        versionException.Which.ConstraintName.Should().Be("publication_log_to_version_id_fkey");
        var deleteActor = () => Db.Learners.ExecuteDeleteAsync();
        var actorException = await deleteActor.Should().ThrowAsync<PostgresException>();
        actorException.Which.ConstraintName.Should().Be("publication_log_actor_learner_id_fkey");
    }

    private static Task<List<string>> ReadSchemaObjectsAsync(AppDbContext db) => db.Database.SqlQueryRaw<string>("""
        SELECT 'relation:' || relname AS "Value" FROM pg_class
        WHERE relnamespace = 'public'::regnamespace
          AND relname <> '__EFMigrationsHistory'
          AND oid NOT IN (
              SELECT indexrelid FROM pg_index WHERE indrelid = '"__EFMigrationsHistory"'::regclass)
        UNION ALL
        SELECT 'constraint:' || conrelid::regclass::text || ':' || conname FROM pg_constraint
        WHERE connamespace = 'public'::regnamespace
          AND conrelid <> '"__EFMigrationsHistory"'::regclass
        """).ToListAsync();
}
