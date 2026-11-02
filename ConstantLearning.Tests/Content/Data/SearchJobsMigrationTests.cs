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

public sealed class SearchJobsMigrationTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;

    [DockerFact]
    public async Task Migrations_ApplyBeforeGroupRetirementAndRestorePreviousSchemaExactly()
    {
        var databaseName = "search_jobs_migrations_" + Guid.NewGuid().ToString("N");
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
                .UseSnakeCaseNamingConvention().Options;
            await using var db = new AppDbContext(options);
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("CreatePublicationLog");
            var baseline = await ReadSchemaAsync(db);
            await migrator.MigrateAsync("AlterContentReportsDedupAndSummary");
            await SearchJobsSchemaAssertions.AssertAsync(db);
            (await db.Database.GetAppliedMigrationsAsync()).TakeLast(6)
                .Select(migration => migration[15..]).Should().Equal(
                    "CreateSearchForms", "CreateMorphForms", "CreateReferenceEntries", "CreateVerificationJobs",
                    "CreateNotificationDispatches", "AlterContentReportsDedupAndSummary");
            var legacyTables = await db.Database.SqlQueryRaw<string>("""
                SELECT tablename AS "Value" FROM pg_tables WHERE schemaname = 'public'
                  AND tablename IN ('Words', 'LearnedWords', 'ChatRegistrations')
                """).ToListAsync();
            legacyTables.Should().BeEquivalentTo("Words", "LearnedWords", "ChatRegistrations");

            await migrator.MigrateAsync("CreatePublicationLog");
            (await ReadSchemaAsync(db)).Should().BeEquivalentTo(baseline,
                "down має відновити колонки, звичайні індекси та FK попередньої схеми");
            await migrator.MigrateAsync(Migration.InitialDatabase);
            (await ReadSchemaAsync(db)).Should().BeEmpty();
            (await db.Database.GetAppliedMigrationsAsync()).Should().BeEmpty();
            await migrator.MigrateAsync("AlterContentReportsDedupAndSummary");
            await SearchJobsSchemaAssertions.AssertAsync(db);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    [DockerFact]
    public async Task Lookups_RoundTripBoundedFieldsUrlsAndNullableMetadata()
    {
        // Довідкові таблиці зберігає harness: транзакція ізолює синтетичні записи цього тесту.
        await using var transaction = await Db.Database.BeginTransactionAsync();
        var form = new MorphForm
        {
            Lang = "pl",
            Source = "test-source",
            Lemma = "żaba",
            Form = "żaby",
            Tags = "noun:plural"
        };
        var withoutTags = new MorphForm { Lang = "uk", Source = "test-source", Lemma = "жаба", Form = "жаби" };
        var reference = new ReferenceEntry
        {
            Source = "test-source",
            Lemma = "żaba",
            LemmaFolded = "zaba",
            Field = "translation",
            Value = "жаба",
            Url = "https://example.test/" + new string('a', 600),
            RevisionId = "test-revision",
            RetrievedAt = Clock.GetCurrentInstant()
        };
        var withoutRevision = new ReferenceEntry
        {
            Source = "test-source",
            Lemma = "żaba",
            LemmaFolded = "zaba",
            Field = "translation",
            Value = "жабка",
            Url = "https://example.test/reference",
            RetrievedAt = Clock.GetCurrentInstant()
        };
        Db.AddRange(form, withoutTags, reference, withoutRevision);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();

        (await Db.MorphForms.SingleAsync(value => value.Id == form.Id)).Should().BeEquivalentTo(form);
        (await Db.MorphForms.SingleAsync(value => value.Id == withoutTags.Id)).Tags.Should().BeNull();
        (await Db.ReferenceEntries.SingleAsync(value => value.Id == reference.Id)).Should().BeEquivalentTo(reference);
        (await Db.ReferenceEntries.SingleAsync(value => value.Id == withoutRevision.Id)).RevisionId.Should().BeNull();
        form.CreatedAt.Should().NotBe(default(Instant));
        reference.CreatedAt.Should().NotBe(default(Instant));
        form.Id.Version.Should().Be(7);
        reference.Id.Version.Should().Be(7);
        await transaction.RollbackAsync();
    }

    [DockerFact]
    public async Task SearchForms_RoundTripPrefixLookupAndCascadeWithVersionAndEntry()
    {
        var root = await ReportsPublicationTestData.CreateRootAsync(Db, Clock);
        var form = NewSearchForm(root.Entry.Id, root.Version.Id);
        Db.SearchForms.Add(form);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();

        var stored = await Db.SearchForms.SingleAsync(value => EF.Functions.Like(value.FormFolded, "zab%"));
        stored.Should().BeEquivalentTo(form);
        stored.CreatedAt.Should().NotBe(default(Instant));
        stored.Id.Version.Should().Be(7);
        await Db.EntryVersions.ExecuteDeleteAsync();
        (await Db.SearchForms.CountAsync()).Should().Be(0);
        Db.Attach(root.Entry);
        var anotherVersion = ReportsPublicationTestData.NewVersion(root.Entry);
        Db.EntryVersions.Add(anotherVersion);
        await Db.SaveChangesAsync();
        Db.SearchForms.Add(NewSearchForm(root.Entry.Id, anotherVersion.Id));
        await Db.SaveChangesAsync();
        await Db.DictionaryEntries.ExecuteDeleteAsync();
        (await Db.SearchForms.CountAsync()).Should().Be(0);
    }

    [DockerFact]
    public async Task VerificationJob_RoundTripsRetryFieldsAndMatchesEventPayloadContract()
    {
        var job = await CreateJobAsync();
        job.LastError.Should().BeNull();
        job.ClaimedAt.Should().BeNull();
        job.CompletedAt.Should().BeNull();
        await AssertJobPayloadAsync(job.Id);
        job.Status = "done";
        job.Attempts = 2;
        job.NextAttemptAt = Clock.GetCurrentInstant() + Duration.FromMinutes(5);
        job.LastError = "Тестова тимчасова помилка";
        job.ClaimedAt = Clock.GetCurrentInstant();
        job.CompletedAt = Clock.GetCurrentInstant() + Duration.FromMinutes(1);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();

        var stored = await Db.VerificationJobs.SingleAsync();
        stored.Should().BeEquivalentTo(job);
        stored.CreatedAt.Should().NotBe(default(Instant));
        stored.Version.Should().NotBe(0);
        stored.Id.Version.Should().Be(7);
        await AssertJobPayloadAsync(job.Id);
    }

    [DockerFact]
    public async Task VerificationJob_IsUniquePerReportAndCascadesWithReport()
    {
        var job = await CreateJobAsync();
        Db.ChangeTracker.Clear();
        Db.VerificationJobs.Add(new VerificationJob
        {
            ContentReportId = job.ContentReportId,
            Status = "pending",
            NextAttemptAt = Clock.GetCurrentInstant()
        });
        var action = () => Db.SaveChangesAsync();
        var exception = await action.Should().ThrowAsync<DbUpdateException>();
        var postgres = exception.Which.InnerException.Should().BeOfType<PostgresException>().Which;
        postgres.ConstraintName.Should().Be("ux_verification_jobs_content_report");
        Db.ChangeTracker.Clear();

        await Db.ContentReports.ExecuteDeleteAsync();
        (await Db.VerificationJobs.CountAsync()).Should().Be(0);
    }

    [DockerFact]
    public async Task VerificationJob_XminRejectsStaleClaim()
    {
        var job = await CreateJobAsync();
        await using (var provider = _postgres.CreateServices(Clock))
        {
            await using var scope = provider.CreateAsyncScope();
            var otherDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var current = await otherDb.VerificationJobs.SingleAsync();
            current.Status = "running";
            await otherDb.SaveChangesAsync();
        }

        job.Status = "dead";
        var action = () => Db.SaveChangesAsync();
        await action.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    private async Task<VerificationJob> CreateJobAsync()
    {
        var root = await ReportsPublicationTestData.CreateRootAsync(Db, Clock);
        var report = ReportsPublicationTestData.NewReport(root);
        Db.ContentReports.Add(report);
        await Db.SaveChangesAsync();
        var job = new VerificationJob
        {
            ContentReportId = report.Id,
            Status = "pending",
            NextAttemptAt = Clock.GetCurrentInstant()
        };
        Db.VerificationJobs.Add(job);
        await Db.SaveChangesAsync();
        return job;
    }

    private async Task AssertJobPayloadAsync(Guid id)
    {
        var json = await Db.Database.SqlQueryRaw<string>("""
            SELECT json_build_object('id', id, 'content_report_id', content_report_id, 'status', status,
                'attempts', attempts, 'next_attempt_at', next_attempt_at)::text AS "Value"
            FROM verification_jobs WHERE id = {0}
            """, id).SingleAsync();
        ContractAssert.EventRow("verification_jobs", json);
    }

    private static SearchForm NewSearchForm(Guid entryId, Guid versionId) => new()
    {
        EntryId = entryId,
        VersionId = versionId,
        Lang = "pl",
        FormDisplay = "żaba",
        FormFolded = "zaba",
        IsLemma = true
    };

    private static Task<List<string>> ReadSchemaAsync(AppDbContext db) => db.Database.SqlQueryRaw<string>("""
        SELECT 'relation:' || relname AS "Value" FROM pg_class
        WHERE relnamespace = 'public'::regnamespace AND relname <> '__EFMigrationsHistory'
          AND oid NOT IN (SELECT indexrelid FROM pg_index WHERE indrelid = '"__EFMigrationsHistory"'::regclass)
        UNION ALL
        SELECT 'column:' || table_name || '.' || column_name || ':' || udt_name
            || COALESCE('(' || character_maximum_length || ')', '')
            || ':' || is_nullable || ':' || COALESCE(column_default, '-')
        FROM information_schema.columns WHERE table_schema = 'public' AND table_name <> '__EFMigrationsHistory'
        UNION ALL
        SELECT 'index:' || indexdef FROM pg_indexes WHERE schemaname = 'public' AND tablename <> '__EFMigrationsHistory'
        UNION ALL
        SELECT 'constraint:' || conrelid::regclass::text || ':' || pg_get_constraintdef(oid)
        FROM pg_constraint WHERE connamespace = 'public'::regnamespace
          AND conrelid <> '"__EFMigrationsHistory"'::regclass
        """).ToListAsync();
}
