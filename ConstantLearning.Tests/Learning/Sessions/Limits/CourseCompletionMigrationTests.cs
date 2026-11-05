using ConstantLearning.Platform.Persistence;
using ConstantLearning.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace ConstantLearning.Tests.Learning.Sessions.Limits;

public sealed class CourseCompletionMigrationTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;

    [DockerFact]
    public async Task NullableMarkerPreservesExistingLearnerWithoutBackfill_AndSupportsCleanRollbackAndReapply()
    {
        var databaseName = "course_completion_migration_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(_postgres.ConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin))
            await create.ExecuteNonQueryAsync();

        try
        {
            var connectionString = new NpgsqlConnectionStringBuilder(_postgres.ConnectionString)
            {
                Database = databaseName,
                Pooling = false
            }.ConnectionString;
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connectionString, npgsql => npgsql.UseNodaTime()).UseSnakeCaseNamingConvention().Options;
            await using var db = new AppDbContext(options);
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("PersistCurrentLearnerDay");
            var before = await ReadColumnsAsync(db);
            var constraints = await ReadConstraintsAsync(db);
            var learnerId = Guid.CreateVersion7();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO learners (id, telegram_user_id, time_zone, time_zone_confirmed, daily_new_words_limit, can_message)
                VALUES ({learnerId}, 940000001, 'Europe/Warsaw', true, 5, true)
                """);
            await migrator.MigrateAsync("PersistCourseCompletion");
            var after = await ReadColumnsAsync(db);
            Assert.Equal(new[] { "course_completed_at:timestamptz:YES:-" }, after.Except(before));
            Assert.Equal(constraints, await ReadConstraintsAsync(db));
            // Читаємо сирим SQL, а не через сутність Learner: на цій версії схеми ще немає колонки
            // active_target_language (її додає пізніша міграція CreateTargetLanguages), тож запит сутності впав би.
            var persisted = await db.Database.SqlQueryRaw<string>("""
                SELECT id::text || ':' || daily_new_words_limit::text || ':' || COALESCE(course_completed_at::text, '-') AS "Value"
                FROM learners
                """).SingleAsync();
            Assert.Equal($"{learnerId}:5:-", persisted);
            Assert.False(db.Database.HasPendingModelChanges());

            await migrator.MigrateAsync("PersistCurrentLearnerDay");
            Assert.Equal(before, await ReadColumnsAsync(db));
            Assert.Equal(5, await db.Database.SqlQueryRaw<short>("""SELECT daily_new_words_limit AS "Value" FROM learners""").SingleAsync());
            await migrator.MigrateAsync("PersistCourseCompletion");
            Assert.Equal(after, await ReadColumnsAsync(db));
            await migrator.MigrateAsync(Migration.InitialDatabase);
            Assert.Empty(await db.Database.GetAppliedMigrationsAsync());
            await migrator.MigrateAsync("PersistCourseCompletion");
            Assert.Equal(after, await ReadColumnsAsync(db));
            Assert.Equal(constraints, await ReadConstraintsAsync(db));
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static Task<List<string>> ReadColumnsAsync(AppDbContext db) => db.Database.SqlQueryRaw<string>("""
        SELECT column_name || ':' || udt_name || ':' || is_nullable || ':' || COALESCE(column_default, '-') AS "Value"
        FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'learners'
        ORDER BY column_name
        """).ToListAsync();

    private static Task<List<string>> ReadConstraintsAsync(AppDbContext db) => db.Database.SqlQueryRaw<string>("""
        SELECT conname AS "Value" FROM pg_constraint WHERE conrelid = 'learners'::regclass ORDER BY conname
        """).ToListAsync();
}
