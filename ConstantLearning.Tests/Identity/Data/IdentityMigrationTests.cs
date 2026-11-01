using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Platform.Persistence;
using ConstantLearning.Tests.Fixtures;
using ConstantLearning.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Npgsql;

namespace ConstantLearning.Tests.Identity.Data;

public sealed class IdentityMigrationTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;

    [DockerFact]
    public async Task Migrations_ApplyOnEmptyDatabaseAndRollbackWithoutRelations()
    {
        var databaseName = "identity_migrations_" + Guid.NewGuid().ToString("N");
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

            await migrator.MigrateAsync("CreateReminderSchedules");
            await AssertIdentitySchemaAsync(db);
            await migrator.MigrateAsync(Migration.InitialDatabase);

            var remainingRelations = await db.Database.SqlQueryRaw<string>("""
                SELECT relname AS "Value"
                FROM pg_class
                WHERE relnamespace = 'public'::regnamespace
                  AND relname <> '__EFMigrationsHistory'
                  AND oid NOT IN (
                      SELECT indexrelid FROM pg_index WHERE indrelid = '"__EFMigrationsHistory"'::regclass)
                """).ToListAsync();
            remainingRelations.Should().BeEmpty("відкат повинен видалити таблиці, індекси й послідовності");
            (await db.Database.GetAppliedMigrationsAsync()).Should().BeEmpty();

            await migrator.MigrateAsync("CreateReminderSchedules");
            await AssertIdentitySchemaAsync(db);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    [DockerFact]
    public async Task Identity_RoundTripsTimeAndNullsAndCascadesReminderDeletion()
    {
        var learner = NewLearner();
        learner.PendingDailyNewWordsLimit = 10;
        learner.PendingLimitEffectiveFrom = Clock.GetCurrentInstant() + Duration.FromHours(14);
        learner.NewWordsAvailableFrom = Clock.GetCurrentInstant() + Duration.FromHours(15);
        learner.OnboardedAt = Clock.GetCurrentInstant();
        learner.ReminderSchedule = new ReminderSchedule
        {
            LocalTime = new LocalTime(8, 30, 15),
            DaysOfWeek = 31
        };
        var otherLearner = NewLearner();
        otherLearner.FirstName = null;
        Db.Learners.AddRange(learner, otherLearner);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();

        var stored = await Db.Learners.Include(item => item.ReminderSchedule)
            .SingleAsync(item => item.Id == learner.Id);
        stored.TelegramUserId.Should().Be(learner.TelegramUserId);
        stored.TimeZone.Should().Be("Europe/Warsaw");
        stored.TimeZoneConfirmed.Should().BeTrue();
        stored.DailyNewWordsLimit.Should().Be(5);
        stored.PendingDailyNewWordsLimit.Should().Be(10);
        stored.PendingLimitEffectiveFrom.Should().Be(learner.PendingLimitEffectiveFrom);
        stored.NewWordsAvailableFrom.Should().Be(learner.NewWordsAvailableFrom);
        stored.OnboardedAt.Should().Be(learner.OnboardedAt);
        stored.CanMessage.Should().BeTrue();
        stored.CreatedAt.Should().NotBe(default(Instant));
        stored.Version.Should().NotBe(0);
        stored.ReminderSchedule!.LocalTime.Should().Be(new LocalTime(8, 30, 15));
        stored.ReminderSchedule.DaysOfWeek.Should().Be(31);
        stored.ReminderSchedule.CreatedAt.Should().NotBe(default(Instant));
        stored.ReminderSchedule.Version.Should().NotBe(0);
        var otherStored = await Db.Learners.SingleAsync(item => item.Id == otherLearner.Id);
        otherStored.FirstName.Should().BeNull();
        otherStored.ReporterAlias.Should().BeNull();
        otherStored.PendingDailyNewWordsLimit.Should().BeNull();
        otherStored.PendingLimitEffectiveFrom.Should().BeNull();
        otherStored.NewWordsAvailableFrom.Should().BeNull();
        otherStored.OnboardedAt.Should().BeNull();

        // Видалення без завантаженої навігації перевіряє саме FK у PostgreSQL.
        Db.ChangeTracker.Clear();
        await Db.Learners.Where(item => item.Id == learner.Id).ExecuteDeleteAsync();
        (await Db.ReminderSchedules.CountAsync()).Should().Be(0);
        (await Db.Learners.CountAsync()).Should().Be(1);
    }

    [DockerFact]
    public async Task Learners_RejectDuplicateTelegramIdentity()
    {
        var learner = NewLearner();
        Db.Learners.Add(learner);
        await Db.SaveChangesAsync();
        var duplicate = NewLearner();
        duplicate.TelegramUserId = learner.TelegramUserId;
        Db.Learners.Add(duplicate);

        var action = () => Db.SaveChangesAsync();
        var exception = await action.Should().ThrowAsync<DbUpdateException>();
        exception.Which.InnerException.Should().BeOfType<PostgresException>()
            .Which.ConstraintName.Should().Be("ux_learners_telegram_user_id");
    }

    [DockerFact]
    public async Task Learners_RejectDuplicateNonNullReporterAlias()
    {
        var learner = NewLearner();
        learner.ReporterAlias = "test-alias";
        Db.Learners.Add(learner);
        await Db.SaveChangesAsync();
        var duplicate = NewLearner();
        duplicate.ReporterAlias = learner.ReporterAlias;
        Db.Learners.Add(duplicate);

        var action = () => Db.SaveChangesAsync();
        var exception = await action.Should().ThrowAsync<DbUpdateException>();
        exception.Which.InnerException.Should().BeOfType<PostgresException>()
            .Which.ConstraintName.Should().Be("ux_learners_reporter_alias");
    }

    [DockerFact]
    public async Task Learner_XminRejectsStaleUpdate()
    {
        var learner = NewLearner();
        Db.Learners.Add(learner);
        await Db.SaveChangesAsync();

        await using (var provider = _postgres.CreateServices(Clock))
        {
            await using var scope = provider.CreateAsyncScope();
            var otherDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var current = await otherDb.Learners.SingleAsync(item => item.Id == learner.Id);
            current.TimeZone = "Europe/Kyiv";
            await otherDb.SaveChangesAsync();
        }

        learner.DailyNewWordsLimit = 10;
        var action = () => Db.SaveChangesAsync();
        await action.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    private Learner NewLearner()
    {
        var data = LearnerFixtures.NewLearner(clock: Clock);
        return new Learner
        {
            Id = data.Id,
            TelegramUserId = data.TelegramUserId,
            FirstName = data.FirstName,
            TimeZone = data.TimeZone,
            TimeZoneConfirmed = data.TimeZoneConfirmed,
            DailyNewWordsLimit = checked((short)data.DailyNewWordsLimit),
            CanMessage = true
        };
    }

    private static async Task AssertIdentitySchemaAsync(AppDbContext db)
    {
        var columns = await db.Database.SqlQueryRaw<string>("""
            SELECT table_name || '.' || column_name || ':' || udt_name
                || COALESCE('(' || character_maximum_length || ')', '')
                || ':' || is_nullable || ':' || COALESCE(column_default, '-') AS "Value"
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name IN ('learners', 'reminder_schedules')
            """).ToListAsync();
        columns.Should().BeEquivalentTo(new[]
        {
            "learners.id:uuid:NO:-",
            "learners.telegram_user_id:int8:NO:-",
            "learners.first_name:varchar(64):YES:-",
            "learners.time_zone:varchar(64):NO:-",
            "learners.time_zone_confirmed:bool:NO:-",
            "learners.daily_new_words_limit:int2:NO:-",
            "learners.pending_daily_new_words_limit:int2:YES:-",
            "learners.pending_limit_effective_from:timestamptz:YES:-",
            "learners.new_words_available_from:timestamptz:YES:-",
            "learners.reporter_alias:varchar(16):YES:-",
            "learners.can_message:bool:NO:-",
            "learners.onboarded_at:timestamptz:YES:-",
            "learners.created_at:timestamptz:NO:now()",
            "reminder_schedules.learner_id:uuid:NO:-",
            "reminder_schedules.local_time:time:NO:-",
            "reminder_schedules.days_of_week:int2:NO:-",
            "reminder_schedules.created_at:timestamptz:NO:now()"
        });

        var indexes = await db.Database.SqlQueryRaw<string>("""
            SELECT indexname AS "Value" FROM pg_indexes
            WHERE schemaname = 'public' AND tablename IN ('learners', 'reminder_schedules')
            """).ToListAsync();
        indexes.Should().BeEquivalentTo(new[]
        {
            "learners_pkey", "ux_learners_telegram_user_id", "ux_learners_reporter_alias",
            "reminder_schedules_pkey"
        });
        var constraints = await db.Database.SqlQueryRaw<string>("""
            SELECT conname AS "Value" FROM pg_constraint
            WHERE conrelid IN ('learners'::regclass, 'reminder_schedules'::regclass)
              AND contype IN ('p', 'f', 'u', 'c')
            """).ToListAsync();
        constraints.Should().BeEquivalentTo(new[]
        {
            "learners_pkey", "reminder_schedules_pkey", "reminder_schedules_learner_id_fkey"
        });
    }
}
