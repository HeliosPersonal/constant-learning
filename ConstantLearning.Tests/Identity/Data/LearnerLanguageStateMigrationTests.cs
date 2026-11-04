using ConstantLearning.Platform.Persistence;
using ConstantLearning.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace ConstantLearning.Tests.Identity.Data;

public sealed class LearnerLanguageStateMigrationTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;

    [DockerFact]
    public async Task CreateLearnerLanguageStates_CreatesSchemaBackfillsPl_AndRollsBack()
    {
        var databaseName = "learner_language_states_migrations_" + Guid.NewGuid().ToString("N");
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

            // Наявний learner до T03 — щоб перевірити беклфіл обох кроків.
            await migrator.MigrateAsync("CreateTargetLanguages");
            var learnerId = Guid.CreateVersion7();
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO learners (
                    id, telegram_user_id, time_zone, time_zone_confirmed, daily_new_words_limit, can_message)
                VALUES ({0}, 42, 'Europe/Kyiv', TRUE, 5, TRUE);
                """, learnerId);

            await migrator.MigrateAsync("IndexLearnersActiveTargetLanguage");

            var columns = await db.Database.SqlQueryRaw<string>("""
                SELECT table_name || '.' || column_name || ':' || udt_name
                    || COALESCE('(' || character_maximum_length || ')', '')
                    || ':' || is_nullable || ':' || COALESCE(column_default, '-') AS "Value"
                FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = 'learner_language_states'
                ORDER BY ordinal_position
                """).ToListAsync();
            columns.Should().Equal(
                "learner_language_states.learner_id:uuid:NO:-",
                "learner_language_states.target_language:varchar(2):NO:-",
                "learner_language_states.daily_new_words_limit:int2:NO:-",
                "learner_language_states.pending_daily_new_words_limit:int2:YES:-",
                "learner_language_states.pending_limit_effective_from:timestamptz:YES:-",
                "learner_language_states.new_words_available_from:timestamptz:YES:-",
                "learner_language_states.current_day_date:date:YES:-",
                "learner_language_states.current_day_starts_at:timestamptz:YES:-",
                "learner_language_states.current_day_ends_at:timestamptz:YES:-",
                "learner_language_states.current_day_original_ends_at:timestamptz:YES:-",
                "learner_language_states.current_day_time_zone_at_start:varchar(64):YES:-",
                "learner_language_states.course_completed_at:timestamptz:YES:-",
                "learner_language_states.created_at:timestamptz:NO:now()");

            var activeColumn = await db.Database.SqlQueryRaw<string>("""
                SELECT column_name || ':' || udt_name
                    || COALESCE('(' || character_maximum_length || ')', '') || ':' || is_nullable AS "Value"
                FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = 'learners'
                    AND column_name = 'active_target_language'
                """).SingleAsync();
            activeColumn.Should().Be("active_target_language:varchar(2):YES");

            // Беклфіл: наявний learner отримав стан 'pl' і активну мову 'pl'.
            var stateRows = await db.Database.SqlQueryRaw<string>("""
                SELECT target_language || ':' || daily_new_words_limit AS "Value"
                FROM learner_language_states WHERE learner_id = {0}
                """, learnerId).ToListAsync();
            stateRows.Should().Equal("pl:5");
            var active = await db.Database.SqlQueryRaw<string>("""
                SELECT active_target_language AS "Value" FROM learners WHERE id = {0}
                """, learnerId).SingleAsync();
            active.Should().Be("pl");

            var indexes = await db.Database.SqlQueryRaw<string>("""
                SELECT indexname AS "Value" FROM pg_indexes
                WHERE schemaname = 'public'
                    AND indexname IN ('ix_learner_language_states_target_language', 'ix_learners_active_target_language')
                ORDER BY indexname
                """).ToListAsync();
            indexes.Should().Equal("ix_learner_language_states_target_language", "ix_learners_active_target_language");

            var foreignKeys = await db.Database.SqlQueryRaw<string>("""
                SELECT conname AS "Value" FROM pg_constraint
                WHERE contype = 'f' AND conname IN (
                    'learner_language_states_learner_id_fkey',
                    'learner_language_states_target_language_fkey',
                    'learners_active_target_language_fkey')
                ORDER BY conname
                """).ToListAsync();
            foreignKeys.Should().Equal(
                "learner_language_states_learner_id_fkey",
                "learner_language_states_target_language_fkey",
                "learners_active_target_language_fkey");

            await migrator.MigrateAsync("CreateTargetLanguages");

            var reverted = await db.Database.SqlQueryRaw<string>("""
                SELECT COALESCE(
                    (to_regclass('public.learner_language_states') IS NOT NULL)::text || ':' ||
                    EXISTS(SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'learners' AND column_name = 'active_target_language')::text,
                    'error') AS "Value"
                """).SingleAsync();
            reverted.Should().Be("False:False", "відкат T03 має прибрати таблицю та колонку active_target_language");
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
