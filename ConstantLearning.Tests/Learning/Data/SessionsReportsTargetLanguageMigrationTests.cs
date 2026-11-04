using ConstantLearning.Platform.Persistence;
using ConstantLearning.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace ConstantLearning.Tests.Learning.Data;

public sealed class SessionsReportsTargetLanguageMigrationTests(PostgresFixture postgres)
    : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;

    [DockerFact]
    public async Task MigrateSessionsReports_AddsDiscriminatorSwapsActiveUnique_AndRollsBack()
    {
        var databaseName = "sessions_reports_lang_migrations_" + Guid.NewGuid().ToString("N");
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

            // Стан до T09: session/report ще без target_language, глобальний UNIQUE активної session чинний.
            await migrator.MigrateAsync("MigrateLearnerProgressTargetLanguage");

            var learnerId = Guid.CreateVersion7();
            var sessionId = Guid.CreateVersion7();
            var reportId = Guid.CreateVersion7();

            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO learners (
                    id, telegram_user_id, time_zone, time_zone_confirmed,
                    daily_new_words_limit, can_message)
                VALUES ({0}, 900000002, 'Europe/Warsaw', true, 5, true);
                """, learnerId);
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO learning_sessions (
                    id, learner_id, learner_day, ends_at, status, kind)
                VALUES ({0}, {1}, CURRENT_DATE, now() + interval '1 hour', 'active', 'plan');
                """, sessionId, learnerId);
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO content_reports (id, learner_id, origin, status)
                VALUES ({0}, {1}, 'report', 'open');
                """, reportId, learnerId);

            await migrator.MigrateAsync("MigrateSessionsReportsTargetLanguage");

            // Колонки — varchar(2) NOT NULL без дефолту на обох таблицях.
            var columns = await db.Database.SqlQueryRaw<string>("""
                SELECT table_name || '.' || column_name || ':' || udt_name
                    || COALESCE('(' || character_maximum_length || ')', '')
                    || ':' || is_nullable || ':' || COALESCE(column_default, '-') AS "Value"
                FROM information_schema.columns
                WHERE table_schema = 'public' AND column_name = 'target_language'
                    AND table_name IN ('learning_sessions', 'content_reports')
                ORDER BY table_name
                """).ToListAsync();
            columns.Should().Equal(
                "content_reports.target_language:varchar(2):NO:-",
                "learning_sessions.target_language:varchar(2):NO:-");

            // Беклфіл 'pl' на обох рядках.
            var backfilled = await db.Database.SqlQueryRaw<string>("""
                SELECT (SELECT target_language FROM learning_sessions WHERE id = {0})
                    || ':' || (SELECT target_language FROM content_reports WHERE id = {1}) AS "Value"
                """, sessionId, reportId).SingleAsync();
            backfilled.Should().Be("pl:pl");

            // Своп UNIQUE активної session: мово-свідомий присутній, глобальний зник.
            var indexes = await db.Database.SqlQueryRaw<string>("""
                SELECT indexname AS "Value" FROM pg_indexes
                WHERE schemaname = 'public'
                    AND indexname IN (
                        'ix_learning_sessions_target_language', 'ux_learning_sessions_one_active_lang',
                        'ux_learning_sessions_one_active', 'ix_content_reports_target_language',
                        'ix_content_reports_lang_status_created')
                ORDER BY indexname
                """).ToListAsync();
            indexes.Should().Equal(
                "ix_content_reports_lang_status_created", "ix_content_reports_target_language",
                "ix_learning_sessions_target_language", "ux_learning_sessions_one_active_lang");

            var foreignKeys = await db.Database.SqlQueryRaw<string>("""
                SELECT conname AS "Value" FROM pg_constraint
                WHERE contype = 'f'
                    AND conname IN (
                        'learning_sessions_target_language_fkey', 'content_reports_target_language_fkey')
                ORDER BY conname
                """).ToListAsync();
            foreignKeys.Should().Equal(
                "content_reports_target_language_fkey", "learning_sessions_target_language_fkey");

            await migrator.MigrateAsync("MigrateLearnerProgressTargetLanguage");

            var reverted = await db.Database.SqlQueryRaw<string>("""
                SELECT (
                    EXISTS(SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'learning_sessions' AND column_name = 'target_language'))::text
                    || ':' || (EXISTS(SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'content_reports' AND column_name = 'target_language'))::text
                    || ':' || (EXISTS(SELECT 1 FROM pg_indexes
                        WHERE indexname = 'ux_learning_sessions_one_active'))::text
                    || ':' || (EXISTS(SELECT 1 FROM pg_indexes
                        WHERE indexname = 'ux_learning_sessions_one_active_lang'))::text AS "Value"
                """).SingleAsync();
            reverted.Should().Be("False:False:True:False",
                "відкат T09 має прибрати target_language й мовні індекси та відновити глобальний UNIQUE активної session");
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
