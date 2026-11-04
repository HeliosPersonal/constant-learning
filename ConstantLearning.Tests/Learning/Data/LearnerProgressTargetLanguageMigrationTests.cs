using ConstantLearning.Platform.Persistence;
using ConstantLearning.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace ConstantLearning.Tests.Learning.Data;

public sealed class LearnerProgressTargetLanguageMigrationTests(PostgresFixture postgres)
    : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;

    [DockerFact]
    public async Task MigrateLearnerProgress_AddsDiscriminatorBackfilledFromEntry_AndRollsBack()
    {
        var databaseName = "learner_progress_lang_migrations_" + Guid.NewGuid().ToString("N");
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

            // Стан до T08: dictionary_entries вже має target_language (T05), прогрес — ще ні.
            await migrator.MigrateAsync("MigrateDictionaryEntriesTargetLanguage");

            // Запис мовою 'en' (не 'pl') — щоб довести, що беклфіл виводить мову з entry_id,
            // а не зашиває дефолт 'pl'.
            var entryId = Guid.CreateVersion7();
            var versionId = Guid.CreateVersion7();
            var learnerId = Guid.CreateVersion7();
            var learnerWordId = Guid.CreateVersion7();
            var answerEventId = Guid.CreateVersion7();

            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO dictionary_entries (id, external_id, domain, target_language)
                VALUES ({0}, 'legacy-en-word', 'everyday', 'en');
                """, entryId);
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO entry_versions (
                    id, entry_id, lemma_pl, part_of_speech, pronunciation_uk,
                    example_pl, example_uk, show_context_before, validation_status,
                    created_by_kind, content_hash)
                VALUES ({0}, {1}, 'kot', 'noun', 'кот', 'To jest kot.', 'Це кіт.',
                    false, 'accepted', 'methodist', 'hash-en');
                """, versionId, entryId);
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO learners (
                    id, telegram_user_id, time_zone, time_zone_confirmed,
                    daily_new_words_limit, can_message)
                VALUES ({0}, 900000001, 'Europe/Warsaw', true, 5, true);
                """, learnerId);
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO learner_words (
                    id, learner_id, entry_id, status, consecutive_scheduled_correct,
                    control_pending, lapses, model_state, scheduler_version)
                VALUES ({0}, {1}, {2}, 'learning', 0, false, 0, '{{}}', 'v1');
                """, learnerWordId, learnerId, entryId);
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO answer_events (
                    id, learner_id, entry_id, version_id, exercise_kind, outcome,
                    answered_at, learner_day, time_zone, scheduler_version, state_before)
                VALUES ({0}, {1}, {2}, {3}, 'type_answer', 'correct',
                    now(), CURRENT_DATE, 'Europe/Warsaw', 'v1', '{{}}');
                """, answerEventId, learnerId, entryId, versionId);

            await migrator.MigrateAsync("MigrateLearnerProgressTargetLanguage");

            // Колонки — varchar(2) NOT NULL без дефолту на обох таблицях.
            var columns = await db.Database.SqlQueryRaw<string>("""
                SELECT table_name || '.' || column_name || ':' || udt_name
                    || COALESCE('(' || character_maximum_length || ')', '')
                    || ':' || is_nullable || ':' || COALESCE(column_default, '-') AS "Value"
                FROM information_schema.columns
                WHERE table_schema = 'public' AND column_name = 'target_language'
                    AND table_name IN ('learner_words', 'answer_events')
                ORDER BY table_name
                """).ToListAsync();
            columns.Should().Equal(
                "answer_events.target_language:varchar(2):NO:-",
                "learner_words.target_language:varchar(2):NO:-");

            // Беклфіл: обидва рядки успадкували 'en' свого entry.
            var backfilled = await db.Database.SqlQueryRaw<string>("""
                SELECT (SELECT target_language FROM learner_words WHERE id = {0})
                    || ':' || (SELECT target_language FROM answer_events WHERE id = {1}) AS "Value"
                """, learnerWordId, answerEventId).SingleAsync();
            backfilled.Should().Be("en:en", "беклфіл виводить мову з entry_id, а не зашиває 'pl'");

            // Мово-свідомі індекси присутні на обох таблицях.
            var indexes = await db.Database.SqlQueryRaw<string>("""
                SELECT indexname AS "Value" FROM pg_indexes
                WHERE schemaname = 'public'
                    AND indexname IN (
                        'ix_learner_words_target_language', 'ix_learner_words_learner_lang_due',
                        'ix_learner_words_learner_lang_status', 'ix_answer_events_target_language',
                        'ix_answer_events_learner_lang_day_outcome')
                ORDER BY indexname
                """).ToListAsync();
            indexes.Should().Equal(
                "ix_answer_events_learner_lang_day_outcome", "ix_answer_events_target_language",
                "ix_learner_words_learner_lang_due", "ix_learner_words_learner_lang_status",
                "ix_learner_words_target_language");

            var foreignKeys = await db.Database.SqlQueryRaw<string>("""
                SELECT conname AS "Value" FROM pg_constraint
                WHERE contype = 'f'
                    AND conname IN (
                        'learner_words_target_language_fkey', 'answer_events_target_language_fkey')
                ORDER BY conname
                """).ToListAsync();
            foreignKeys.Should().Equal(
                "answer_events_target_language_fkey", "learner_words_target_language_fkey");

            await migrator.MigrateAsync("MigrateDictionaryEntriesTargetLanguage");

            var reverted = await db.Database.SqlQueryRaw<string>("""
                SELECT (
                    EXISTS(SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'learner_words' AND column_name = 'target_language'))::text
                    || ':' || (EXISTS(SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'answer_events' AND column_name = 'target_language'))::text
                    || ':' || (EXISTS(SELECT 1 FROM pg_indexes
                        WHERE indexname = 'ix_learner_words_learner_lang_due'))::text
                    || ':' || (EXISTS(SELECT 1 FROM pg_constraint
                        WHERE conname = 'answer_events_target_language_fkey'))::text AS "Value"
                """).SingleAsync();
            reverted.Should().Be("False:False:False:False",
                "відкат T08 має прибрати target_language, мовні індекси та FK з обох таблиць");
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
