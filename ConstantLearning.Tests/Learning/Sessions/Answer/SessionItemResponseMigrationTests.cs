using ConstantLearning.Platform.Persistence;
using ConstantLearning.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace ConstantLearning.Tests.Learning.Sessions.Answer;

public sealed class SessionItemResponseMigrationTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;

    [DockerFact]
    public async Task NullableOpaqueResponsePreservesOldItemsAndSupportsFullRollbackReapply()
    {
        var databaseName = "session_response_migration_" + Guid.NewGuid().ToString("N");
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
            await migrator.MigrateAsync("PersistCourseCompletion");
            var before = await ReadColumnsAsync(db);
            var constraints = await ReadConstraintsAsync(db);
            var learnerId = Guid.CreateVersion7();
            var entryId = Guid.CreateVersion7();
            var sessionId = Guid.CreateVersion7();
            var itemId = Guid.CreateVersion7();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO learners (id, telegram_user_id, time_zone, time_zone_confirmed, daily_new_words_limit, can_message)
                VALUES ({learnerId}, 960000001, 'Europe/Warsaw', true, 5, true);
                INSERT INTO dictionary_entries (id, external_id, domain, course_position)
                VALUES ({entryId}, 'response-migration-fixture', 'everyday', 1);
                INSERT INTO learning_sessions (id, learner_id, learner_day, ends_at, status)
                VALUES ({sessionId}, {learnerId}, DATE '2026-10-31', TIMESTAMPTZ '2026-10-31 23:00:00Z', 'active');
                INSERT INTO session_items (id, session_id, entry_id, position, kind, state)
                VALUES ({itemId}, {sessionId}, {entryId}, 1, 'review', 'pending');
                """);
            await migrator.MigrateAsync("PersistSessionItemResponse");
            var after = await ReadColumnsAsync(db);
            Assert.Equal(new[] { "response_payload:jsonb:YES:-" }, after.Except(before));
            Assert.Equal(constraints, await ReadConstraintsAsync(db));
            var item = await db.SessionItems.AsNoTracking().SingleAsync();
            Assert.Equal(itemId, item.Id);
            Assert.Equal("pending", item.State);
            Assert.Null(item.ResponsePayload);
            Assert.False(db.Database.HasPendingModelChanges());
            await migrator.MigrateAsync("PersistCourseCompletion");
            Assert.Equal(before, await ReadColumnsAsync(db));
            Assert.Equal(itemId, await db.Database.SqlQueryRaw<Guid>("""SELECT id AS "Value" FROM session_items""").SingleAsync());
            await migrator.MigrateAsync("PersistSessionItemResponse");
            Assert.Equal(after, await ReadColumnsAsync(db));
            await migrator.MigrateAsync(Migration.InitialDatabase);
            Assert.Empty(await db.Database.GetAppliedMigrationsAsync());
            await migrator.MigrateAsync("PersistSessionItemResponse");
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
        FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'session_items'
        ORDER BY column_name
        """).ToListAsync();

    private static Task<List<string>> ReadConstraintsAsync(AppDbContext db) => db.Database.SqlQueryRaw<string>("""
        SELECT conname AS "Value" FROM pg_constraint WHERE conrelid = 'session_items'::regclass ORDER BY conname
        """).ToListAsync();
}
