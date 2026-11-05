using ConstantLearning.Features.Notifications.Data;
using ConstantLearning.Platform.Persistence;
using ConstantLearning.Tests.Fixtures;
using ConstantLearning.Tests.Infrastructure;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace ConstantLearning.Tests.Identity.Data;

/// <summary>
/// ADR-0005: міграція <c>ScopeRemindersPerTargetLanguage</c> переносить наявні нагадування й dispatch-рядки на
/// активну мову learner-а, зберігає унікальність сповіщень без мови і відкочується.
/// </summary>
public sealed class ReminderPerLanguageMigrationTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private const string PreviousMigration = "MigrateSessionsReportsTargetLanguage";
    private readonly PostgresFixture _postgres = postgres;

    [DockerFact]
    public async Task ScopeRemindersMigration_BackfillsActiveLanguage_KeepsDedupeAndRollsBack()
    {
        var databaseName = "reminder_scope_" + Guid.NewGuid().ToString("N");
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
            await migrator.MigrateAsync(PreviousMigration);

            // Стан до ADR-0005: нагадування — по одному на learner (PK learner_id), активна мова — en.
            var learnerId = Guid.CreateVersion7();
            const long telegramUserId = 900000777;
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO learners (id, telegram_user_id, time_zone, time_zone_confirmed, daily_new_words_limit,
                    can_message, active_target_language, onboarded_at, created_at)
                VALUES ({learnerId}, {telegramUserId}, 'Europe/Warsaw', true, 5, true, 'en', now(), now())
                """);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO reminder_schedules (learner_id, local_time, days_of_week, created_at)
                VALUES ({learnerId}, '19:00'::time, 127, now())
                """);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO notification_dispatches (id, recipient_telegram_user_id, learner_id, kind, local_date,
                    due_at, status, attempts, created_at)
                VALUES ({Guid.CreateVersion7()}, {telegramUserId}, {learnerId}, 'reminder', '2026-11-02'::date,
                    now(), 'pending', 0, now())
                """);

            await migrator.MigrateAsync();

            var reminder = await db.ReminderSchedules.AsNoTracking().SingleAsync();
            reminder.LearnerId.Should().Be(learnerId);
            reminder.TargetLanguage.Should().Be("en");
            var dispatch = await db.NotificationDispatches.AsNoTracking().SingleAsync();
            dispatch.TargetLanguage.Should().Be("en");

            // Друга мова того ж дня — окремий рядок; та сама мова — конфлікт; сповіщення без мови — теж унікальні.
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO notification_dispatches (id, recipient_telegram_user_id, learner_id, kind, local_date,
                    due_at, status, attempts, created_at, target_language)
                VALUES ({Guid.CreateVersion7()}, {telegramUserId}, {learnerId}, 'reminder', '2026-11-02'::date,
                    now(), 'pending', 0, now(), 'pl')
                """);
            await AssertDuplicateAsync(db, "reminder", "en", learnerId, telegramUserId);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO notification_dispatches (id, recipient_telegram_user_id, learner_id, kind, local_date,
                    due_at, status, attempts, created_at)
                VALUES ({Guid.CreateVersion7()}, {telegramUserId}, NULL, 'summary', '2026-11-02'::date,
                    now(), 'pending', 0, now())
                """);
            await AssertDuplicateAsync(db, "summary", null, null, telegramUserId);

            await migrator.MigrateAsync(PreviousMigration);
            var rollbackCount = await db.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*)::int AS \"Value\" FROM reminder_schedules").SingleAsync();
            rollbackCount.Should().Be(1, "відкат лишає нагадування активної мови");
            var rollbackDispatch = await db.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*)::int AS \"Value\" FROM notification_dispatches WHERE kind = 'reminder'").SingleAsync();
            rollbackDispatch.Should().Be(1, "відкат видаляє сповіщення неактивних мов");
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static async Task AssertDuplicateAsync(AppDbContext db, string kind, string? language, Guid? learnerId,
        long telegramUserId)
    {
        var duplicate = () => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO notification_dispatches (id, recipient_telegram_user_id, learner_id, kind, local_date,
                due_at, status, attempts, created_at, target_language)
            VALUES ({Guid.CreateVersion7()}, {telegramUserId}, {learnerId}, {kind}, '2026-11-02'::date,
                now(), 'pending', 0, now(), {language})
            """);
        await duplicate.Should().ThrowAsync<Npgsql.PostgresException>()
            .Where(exception => exception.SqlState == "23505");
    }
}
