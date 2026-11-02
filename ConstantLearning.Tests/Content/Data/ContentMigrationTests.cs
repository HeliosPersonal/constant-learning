using ConstantLearning.Features.Content.Data;
using ConstantLearning.Platform.Persistence;
using ConstantLearning.Tests.Fixtures;
using ConstantLearning.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Npgsql;

namespace ConstantLearning.Tests.Content.Data;

public sealed class ContentMigrationTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;

    [DockerFact]
    public async Task Migrations_ApplyInOrderAndRollbackWithoutLeftovers()
    {
        var databaseName = "content_migrations_" + Guid.NewGuid().ToString("N");
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
            await migrator.MigrateAsync("CreateReminderSchedules");
            var baseline = await ReadRelationsAsync(db);

            await migrator.MigrateAsync("CreateVersionAcceptedAnswers");
            await ContentSchemaAssertions.AssertAsync(db);
            (await db.Database.GetAppliedMigrationsAsync()).TakeLast(5)
                .Select(migration => migration[15..]).Should().Equal(
                    "CreateDictionaryEntries", "CreateEntryVersions",
                    "AddPublishedVersionForeignKeyToDictionaryEntries", "CreateVersionTranslations",
                    "CreateVersionAcceptedAnswers");

            await migrator.MigrateAsync("CreateReminderSchedules");
            (await ReadRelationsAsync(db)).Should().BeEquivalentTo(baseline,
                "п'ять down-блоків повинні повністю повернути попередню схему");
            await migrator.MigrateAsync(Migration.InitialDatabase);
            (await ReadRelationsAsync(db)).Should().BeEmpty();
            (await db.Database.GetAppliedMigrationsAsync()).Should().BeEmpty();
            await migrator.MigrateAsync("CreateVersionAcceptedAnswers");
            await ContentSchemaAssertions.AssertAsync(db);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    [DockerFact]
    public async Task Content_RoundTripsVersionHistoryAndMovesPublishedPointer()
    {
        var (entry, original) = await SaveVersionAsync(version =>
        {
            version.Gender = "f";
            version.GenderPairId = "test-gender-pair";
            version.AspectPairId = "test-aspect-pair";
            version.ShowContextBefore = true;
            version.FrequencyRank = 42;
            version.UtilityRank = 7;
            version.LegalizationStage = "test-stage";
            version.Topic = "test-topic";
            version.ReviewNote = "Тестова примітка";
        });
        var revised = NewVersion(entry);
        revised.BasedOnVersionId = original.Id;
        revised.ContentHash = new string('b', 64);
        Db.EntryVersions.Add(revised);
        var originalTranslation = NewTranslation(original.Id);
        var revisedTranslation = NewTranslation(revised.Id);
        revisedTranslation.TextUk = "жабка";
        Db.VersionTranslations.AddRange(originalTranslation, revisedTranslation);
        Db.VersionAcceptedAnswers.AddRange(NewAnswer(original.Id), NewAnswer(revised.Id));
        await Db.SaveChangesAsync();
        entry.PublishedVersionId = revised.Id;
        entry.CoursePosition = 1;
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();

        var stored = await Db.DictionaryEntries.Include(item => item.PublishedVersion).SingleAsync();
        stored.Id.Should().Be(entry.Id);
        stored.ExternalId.Should().Be(entry.ExternalId);
        stored.Domain.Should().Be("everyday");
        stored.CoursePosition.Should().Be(1);
        stored.PublishedVersionId.Should().Be(revised.Id);
        stored.CreatedAt.Should().NotBe(default(Instant));
        stored.Version.Should().NotBe(0);
        stored.PublishedVersion.Should().BeEquivalentTo(revised, options => options
            .Excluding(version => version.Entry).Excluding(version => version.BasedOnVersion));
        var storedOriginal = await Db.EntryVersions.SingleAsync(item => item.Id == original.Id);
        storedOriginal.Should().BeEquivalentTo(original, options => options
            .Excluding(version => version.Entry).Excluding(version => version.BasedOnVersion));
        storedOriginal.CreatedAt.Should().NotBe(default(Instant));
        storedOriginal.Version.Should().NotBe(0);
        (await Db.VersionTranslations.OrderBy(item => item.TextUk).Select(item => item.TextUk).ToListAsync())
            .Should().BeEquivalentTo("жаба", "жабка");
        (await Db.VersionTranslations.AllAsync(item => item.CreatedAt != default)).Should().BeTrue();
        (await Db.VersionAcceptedAnswers.CountAsync(item => item.TextPl == "żaba" && item.IsCanonical))
            .Should().Be(2);
        (await Db.VersionAcceptedAnswers.AllAsync(item => item.CreatedAt != default)).Should().BeTrue();
        originalTranslation.Id.Version.Should().Be(7);
        revised.Id.Version.Should().Be(7);

        stored.PublishedVersionId = original.Id;
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        (await Db.DictionaryEntries.SingleAsync()).PublishedVersionId.Should().Be(original.Id);
        (await Db.EntryVersions.CountAsync()).Should().Be(2);

        // Прямий DELETE перевіряє cascade у БД навіть за циклічного зв'язку публікації.
        await Db.DictionaryEntries.ExecuteDeleteAsync();
        (await Db.EntryVersions.CountAsync()).Should().Be(0);
        (await Db.VersionTranslations.CountAsync()).Should().Be(0);
        (await Db.VersionAcceptedAnswers.CountAsync()).Should().Be(0);
    }

    [DockerFact]
    public async Task DictionaryEntries_RejectDuplicateExternalId()
    {
        var (entry, _) = await SaveVersionAsync();
        Db.DictionaryEntries.Add(new DictionaryEntry { ExternalId = entry.ExternalId, Domain = "everyday" });

        await AssertUniqueViolationAsync("ux_dictionary_entries_external_id");
    }

    [DockerFact]
    public async Task Translations_RejectDuplicatePositionWithinVersion()
    {
        var (_, version) = await SaveVersionAsync();
        Db.VersionTranslations.Add(NewTranslation(version.Id));
        await Db.SaveChangesAsync();
        Db.VersionTranslations.Add(NewTranslation(version.Id));

        await AssertUniqueViolationAsync("ux_version_translations_version_position");
    }

    [DockerFact]
    public async Task AcceptedAnswers_RejectDuplicateTextWithinVersion()
    {
        var (_, version) = await SaveVersionAsync();
        Db.VersionAcceptedAnswers.Add(NewAnswer(version.Id));
        await Db.SaveChangesAsync();
        Db.VersionAcceptedAnswers.Add(NewAnswer(version.Id));

        await AssertUniqueViolationAsync("ux_version_accepted_answers_version_text");
    }

    [DockerFact]
    public async Task PublishedVersion_ForeignKeyPreventsDeletingReferencedVersion()
    {
        var (entry, version) = await SaveVersionAsync();
        entry.PublishedVersionId = version.Id;
        await Db.SaveChangesAsync();

        var action = () => Db.EntryVersions.Where(item => item.Id == version.Id).ExecuteDeleteAsync();
        var exception = await action.Should().ThrowAsync<PostgresException>();
        exception.Which.SqlState.Should().Be(PostgresErrorCodes.ForeignKeyViolation);
        exception.Which.ConstraintName.Should().Be("fk_dictionary_entries_published_version");
    }

    [DockerFact]
    public async Task BasedOnVersion_ForeignKeyPreventsBreakingHistoryChain()
    {
        var (entry, version) = await SaveVersionAsync();
        var revised = NewVersion(entry);
        revised.BasedOnVersionId = version.Id;
        Db.EntryVersions.Add(revised);
        await Db.SaveChangesAsync();

        var action = () => Db.EntryVersions.Where(item => item.Id == version.Id).ExecuteDeleteAsync();
        var exception = await action.Should().ThrowAsync<PostgresException>();
        exception.Which.SqlState.Should().Be(PostgresErrorCodes.ForeignKeyViolation);
        exception.Which.ConstraintName.Should().Be("entry_versions_based_on_version_id_fkey");
    }

    [DockerFact]
    public async Task DictionaryEntry_XminRejectsStalePublishedPointerUpdate()
    {
        var (entry, version) = await SaveVersionAsync();
        await using (var provider = _postgres.CreateServices(Clock))
        {
            await using var scope = provider.CreateAsyncScope();
            var otherDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var current = await otherDb.DictionaryEntries.SingleAsync();
            current.PublishedVersionId = version.Id;
            await otherDb.SaveChangesAsync();
        }

        entry.PublishedVersionId = version.Id;
        var action = () => Db.SaveChangesAsync();
        await action.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [DockerFact]
    public async Task EntryVersion_XminRejectsStaleValidationUpdate()
    {
        var (_, version) = await SaveVersionAsync();
        await using (var provider = _postgres.CreateServices(Clock))
        {
            await using var scope = provider.CreateAsyncScope();
            var otherDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var current = await otherDb.EntryVersions.SingleAsync();
            current.ValidationStatus = "needs_review";
            await otherDb.SaveChangesAsync();
        }

        version.ValidationStatus = "draft";
        var action = () => Db.SaveChangesAsync();
        await action.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    private async Task<(DictionaryEntry Entry, EntryVersion Version)> SaveVersionAsync(
        Action<EntryVersion>? configure = null)
    {
        var data = ContentFixtures.NewPublishedEntry();
        var entry = new DictionaryEntry { Id = data.Id, ExternalId = data.Id.ToString("N"), Domain = "everyday" };
        var version = NewVersion(entry);
        version.Id = data.Version.Id;
        version.LemmaPl = data.Version.LemmaPl;
        configure?.Invoke(version);
        Db.EntryVersions.Add(version);
        await Db.SaveChangesAsync();
        entry.PublishedVersionId.Should().BeNull();
        entry.CoursePosition.Should().BeNull();
        return (entry, version);
    }

    private static EntryVersion NewVersion(DictionaryEntry entry) => new()
    {
        Entry = entry,
        LemmaPl = "żaba",
        PartOfSpeech = "noun",
        PronunciationUk = "жаба",
        ExamplePl = "To jest żaba.",
        ExampleUk = "Це жаба.",
        ValidationStatus = "approved",
        CreatedByKind = "import",
        ContentHash = new string('a', 64)
    };

    private static VersionTranslation NewTranslation(Guid versionId) => new()
    {
        VersionId = versionId,
        Kind = "canonical",
        TextUk = "жаба",
        Position = 0
    };

    private static VersionAcceptedAnswer NewAnswer(Guid versionId) => new()
    {
        VersionId = versionId,
        TextPl = "żaba",
        IsCanonical = true
    };

    private async Task AssertUniqueViolationAsync(string constraint)
    {
        var action = () => Db.SaveChangesAsync();
        var exception = await action.Should().ThrowAsync<DbUpdateException>();
        var postgres = exception.Which.InnerException.Should().BeOfType<PostgresException>().Which;
        postgres.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
        postgres.ConstraintName.Should().Be(constraint);
    }

    private static Task<List<string>> ReadRelationsAsync(AppDbContext db) => db.Database.SqlQueryRaw<string>("""
        SELECT relname AS "Value" FROM pg_class
        WHERE relnamespace = 'public'::regnamespace
          AND relname <> '__EFMigrationsHistory'
          AND oid NOT IN (
              SELECT indexrelid FROM pg_index WHERE indrelid = '"__EFMigrationsHistory"'::regclass)
        """).ToListAsync();
}
