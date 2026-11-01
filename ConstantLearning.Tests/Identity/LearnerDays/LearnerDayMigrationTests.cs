using ConstantLearning.Platform.Persistence;
using ConstantLearning.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace ConstantLearning.Tests.Identity.LearnerDays;

public sealed class LearnerDayMigrationTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;

    [DockerFact]
    public async Task AdditiveMigration_PreservesExistingLearner_AddsNullableColumnsWithoutDefaults_AndRollsBackCleanly()
    {
        var databaseName = "learner_day_migration_" + Guid.NewGuid().ToString("N");
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
                Database = databaseName, Pooling = false
            }.ConnectionString;
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connectionString, npgsql => npgsql.UseNodaTime()).UseSnakeCaseNamingConvention().Options;
            await using var db = new AppDbContext(options);
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("AlterContentReportsDedupAndSummary");
            var before = await ReadColumnsAsync(db);
            var constraintsBefore = await ReadConstraintsAsync(db);
            var learnerId = Guid.CreateVersion7();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO learners (id, telegram_user_id, time_zone, time_zone_confirmed, daily_new_words_limit, can_message)
                VALUES ({learnerId}, 900000001, 'Europe/Warsaw', true, 5, true)
                """);

            await migrator.MigrateAsync("PersistCurrentLearnerDay");
            var after = await ReadColumnsAsync(db);
            Assert.Equal(new[]
            {
                "current_day_date:date:YES:-", "current_day_ends_at:timestamptz:YES:-",
                "current_day_original_ends_at:timestamptz:YES:-", "current_day_starts_at:timestamptz:YES:-",
                "current_day_time_zone_at_start:varchar(64):YES:-"
            }, after.Except(before).Order(StringComparer.Ordinal));
            Assert.Equal(constraintsBefore, await ReadConstraintsAsync(db));
            var learner = await db.Learners.AsNoTracking().SingleAsync();
            Assert.Equal(learnerId, learner.Id);
            Assert.Equal(5, learner.DailyNewWordsLimit);
            Assert.Null(learner.CurrentDayDate);
            Assert.Null(learner.CurrentDayStartsAt);
            Assert.Null(learner.CurrentDayEndsAt);
            Assert.Null(learner.CurrentDayOriginalEndsAt);
            Assert.Null(learner.CurrentDayTimeZoneAtStart);

            await migrator.MigrateAsync("AlterContentReportsDedupAndSummary");
            Assert.Equal(before, await ReadColumnsAsync(db));
            Assert.Equal(5, await db.Database.SqlQueryRaw<short>("""SELECT daily_new_words_limit AS "Value" FROM learners""").SingleAsync());
            await migrator.MigrateAsync("PersistCurrentLearnerDay");
            Assert.Equal(after, await ReadColumnsAsync(db));
            await migrator.MigrateAsync(Migration.InitialDatabase);
            Assert.Empty(await db.Database.GetAppliedMigrationsAsync());
            await migrator.MigrateAsync("PersistCurrentLearnerDay");
            Assert.Equal(after, await ReadColumnsAsync(db));
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static Task<List<string>> ReadColumnsAsync(AppDbContext db) => db.Database.SqlQueryRaw<string>("""
        SELECT column_name || ':' || udt_name || COALESCE('(' || character_maximum_length || ')', '')
               || ':' || is_nullable || ':' || COALESCE(column_default, '-') AS "Value"
        FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'learners'
        ORDER BY column_name
        """).ToListAsync();

    private static Task<List<string>> ReadConstraintsAsync(AppDbContext db) => db.Database.SqlQueryRaw<string>("""
        SELECT conname AS "Value" FROM pg_constraint WHERE conrelid = 'learners'::regclass ORDER BY conname
        """).ToListAsync();
}
