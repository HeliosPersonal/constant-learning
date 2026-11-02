using ConstantLearning.Platform.Persistence;
using ConstantLearning.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace ConstantLearning.Tests.Learning.Disputes;

public sealed class ResultViewMigrationTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;

    [DockerFact]
    public async Task ResultPointerMigrationRollsBackAndReappliesWithMatchingSchema()
    {
        var name = "result_view_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(_postgres.ConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var connection = new NpgsqlConnectionStringBuilder(_postgres.ConnectionString) { Database = name, Pooling = false }.ConnectionString;
            var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection, o => o.UseNodaTime())
                .UseSnakeCaseNamingConvention().Options;
            await using var db = new AppDbContext(options);
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("PersistSessionItemResponse");
            var before = await Schema(db);
            await migrator.MigrateAsync("PersistSessionResultView");
            var after = await Schema(db);
            Assert.Contains(after, line => line.StartsWith("column:result_item_id:", StringComparison.Ordinal));
            Assert.Contains(after, line => line.StartsWith("index:ix_learning_sessions_result_item_id:", StringComparison.Ordinal));
            Assert.Contains(after, line => line.StartsWith("constraint:fk_learning_sessions_result_item_id:", StringComparison.Ordinal));
            Assert.False(db.Database.HasPendingModelChanges());
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            await migrator.MigrateAsync("PersistSessionItemResponse");
            Assert.Equal(before, await Schema(db));
            await migrator.MigrateAsync("PersistSessionResultView");
            Assert.Equal(after, await Schema(db));
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{name}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static Task<List<string>> Schema(AppDbContext db) => db.Database.SqlQueryRaw<string>("""
        SELECT value AS "Value" FROM (
            SELECT 'column:' || column_name || ':' || udt_name || ':' || is_nullable || ':' || COALESCE(column_default, '-') AS value
            FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'learning_sessions'
            UNION ALL SELECT 'index:' || indexname || ':' || indexdef FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'learning_sessions'
            UNION ALL SELECT 'constraint:' || conname || ':' || pg_get_constraintdef(oid) FROM pg_constraint WHERE conrelid = 'learning_sessions'::regclass
        ) schema_items ORDER BY value
        """).ToListAsync();
}
