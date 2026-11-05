using ConstantLearning.Platform.Persistence;
using ConstantLearning.Tests.Infrastructure;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace ConstantLearning.Tests.Identity.Data;

public sealed class TargetLanguageMigrationTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;

    [DockerFact]
    public async Task CreateTargetLanguages_CreatesSchemaAndSeed_AndRollsBack()
    {
        var databaseName = "target_languages_migrations_" + Guid.NewGuid().ToString("N");
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

            await migrator.MigrateAsync("CreateTargetLanguages");

            var columns = await db.Database.SqlQueryRaw<string>("""
                SELECT table_name || '.' || column_name || ':' || udt_name
                    || COALESCE('(' || character_maximum_length || ')', '')
                    || ':' || is_nullable || ':' || COALESCE(column_default, '-') AS "Value"
                FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = 'target_languages'
                ORDER BY ordinal_position
                """).ToListAsync();
            columns.Should().Equal(
                "target_languages.code:varchar(2):NO:-",
                "target_languages.display_name_uk:varchar(64):NO:-",
                "target_languages.is_enabled:bool:NO:-",
                "target_languages.sort_order:int2:NO:-",
                "target_languages.normalization_profile:varchar(32):YES:-",
                "target_languages.created_at:timestamptz:NO:now()");

            var seeded = await db.Database.SqlQueryRaw<string>("""
                SELECT code || ':' || is_enabled || ':' || normalization_profile AS "Value"
                FROM target_languages ORDER BY sort_order
                """).ToListAsync();
            seeded.Should().Equal("pl:true:pl", "en:false:en", "de:false:de", "nl:false:nl");

            await migrator.MigrateAsync("SetKindNotNullOnLearningSessions");

            var exists = await db.Database.SqlQueryRaw<bool>("""
                SELECT to_regclass('public.target_languages') IS NOT NULL AS "Value"
                """).SingleAsync();
            exists.Should().BeFalse("відкат повинен видалити target_languages");
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
