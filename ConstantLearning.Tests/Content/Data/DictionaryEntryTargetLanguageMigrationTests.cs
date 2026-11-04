using ConstantLearning.Platform.Persistence;
using ConstantLearning.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace ConstantLearning.Tests.Content.Data;

public sealed class DictionaryEntryTargetLanguageMigrationTests(PostgresFixture postgres)
    : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;

    [DockerFact]
    public async Task MigrateDictionaryEntries_AddsLanguageDiscriminatorBackfillsPl_AndRollsBack()
    {
        var databaseName = "dictionary_entries_lang_migrations_" + Guid.NewGuid().ToString("N");
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

            // Наявний запис до T05 (ще без target_language) — щоб перевірити беклфіл 'pl'.
            await migrator.MigrateAsync("IndexLearnersActiveTargetLanguage");
            var entryId = Guid.CreateVersion7();
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO dictionary_entries (id, external_id, domain)
                VALUES ({0}, 'legacy-zaba', 'everyday');
                """, entryId);

            await migrator.MigrateAsync("MigrateDictionaryEntriesTargetLanguage");

            var column = await db.Database.SqlQueryRaw<string>("""
                SELECT column_name || ':' || udt_name
                    || COALESCE('(' || character_maximum_length || ')', '')
                    || ':' || is_nullable || ':' || COALESCE(column_default, '-') AS "Value"
                FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = 'dictionary_entries'
                    AND column_name = 'target_language'
                """).SingleAsync();
            column.Should().Be("target_language:varchar(2):NO:-");

            // Беклфіл: наявний запис отримав мову 'pl'.
            var backfilled = await db.Database.SqlQueryRaw<string>("""
                SELECT target_language AS "Value" FROM dictionary_entries WHERE id = {0}
                """, entryId).SingleAsync();
            backfilled.Should().Be("pl");

            // Своп індексів: нові per-language присутні, глобальні зникли.
            var indexes = await db.Database.SqlQueryRaw<string>("""
                SELECT indexname AS "Value" FROM pg_indexes
                WHERE schemaname = 'public'
                    AND indexname IN (
                        'ux_dictionary_entries_lang_external_id', 'ix_dictionary_entries_lang_course_position',
                        'ux_dictionary_entries_external_id', 'ix_dictionary_entries_course_position')
                ORDER BY indexname
                """).ToListAsync();
            indexes.Should().Equal(
                "ix_dictionary_entries_lang_course_position", "ux_dictionary_entries_lang_external_id");

            var foreignKey = await db.Database.SqlQueryRaw<string>("""
                SELECT conname AS "Value" FROM pg_constraint
                WHERE contype = 'f' AND conname = 'dictionary_entries_target_language_fkey'
                """).SingleAsync();
            foreignKey.Should().Be("dictionary_entries_target_language_fkey");

            await migrator.MigrateAsync("IndexLearnersActiveTargetLanguage");

            var reverted = await db.Database.SqlQueryRaw<string>("""
                SELECT (
                    EXISTS(SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'dictionary_entries' AND column_name = 'target_language'))::text
                    || ':' || (EXISTS(SELECT 1 FROM pg_indexes
                        WHERE indexname = 'ux_dictionary_entries_external_id'))::text
                    || ':' || (EXISTS(SELECT 1 FROM pg_indexes
                        WHERE indexname = 'ix_dictionary_entries_course_position'))::text
                    || ':' || (EXISTS(SELECT 1 FROM pg_indexes
                        WHERE indexname = 'ux_dictionary_entries_lang_external_id'))::text AS "Value"
                """).SingleAsync();
            reverted.Should().Be("False:True:True:False",
                "відкат T05 має прибрати target_language й мовні індекси та відновити глобальні");
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
