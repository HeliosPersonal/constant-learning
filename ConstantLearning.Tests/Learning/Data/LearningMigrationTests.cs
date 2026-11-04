using System.Text.Json.Nodes;
using ConstantLearning.Features.Content.Data;
using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Features.Learning.Data;
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

namespace ConstantLearning.Tests.Learning.Data;

public sealed class LearningMigrationTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;

    [DockerFact]
    public async Task Migrations_ApplyInOrderAndRollbackWithoutLeftovers()
    {
        var databaseName = "learning_migrations_" + Guid.NewGuid().ToString("N");
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
            await migrator.MigrateAsync("CreateVersionAcceptedAnswers");
            var baseline = await ReadRelationsAsync(db);
            await migrator.MigrateAsync("CreateAnswerEvents");
            await LearningSchemaAssertions.AssertAsync(db);
            (await db.Database.GetAppliedMigrationsAsync()).TakeLast(4)
                .Select(migration => migration[15..]).Should().Equal(
                    "CreateLearnerWords", "CreateLearningSessions", "CreateSessionItems", "CreateAnswerEvents");

            await migrator.MigrateAsync("CreateVersionAcceptedAnswers");
            (await ReadRelationsAsync(db)).Should().BeEquivalentTo(baseline,
                "чотири down-блоки повинні повністю повернути попередню схему");
            await migrator.MigrateAsync(Migration.InitialDatabase);
            (await ReadRelationsAsync(db)).Should().BeEmpty();
            (await db.Database.GetAppliedMigrationsAsync()).Should().BeEmpty();
            await migrator.MigrateAsync("CreateAnswerEvents");
            await LearningSchemaAssertions.AssertAsync(db);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    [DockerFact]
    public async Task Learning_RoundTripsDatesInstantsJsonAndNullableFields()
    {
        var scenario = await SaveScenarioAsync();
        var word = scenario.Word;
        word.FirstShownDay.Should().BeNull();
        word.LastReviewDay.Should().BeNull();
        word.DueOn.Should().BeNull();
        word.IntervalDays.Should().BeNull();
        scenario.Session.LastActivityAt.Should().BeNull();
        scenario.Session.CompletedAt.Should().BeNull();
        scenario.Item.AnswerPayloadHash.Should().BeNull();
        scenario.Item.AnsweredAt.Should().BeNull();
        word.ConsecutiveScheduledCorrect = 2;
        word.FirstShownDay = new LocalDate(2026, 10, 24);
        word.LastReviewDay = scenario.Session.LearnerDay;
        word.IntervalDays = 3;
        word.DueOn = scenario.Session.LearnerDay.PlusDays(3);
        word.ControlPending = true;
        word.Lapses = 1;
        word.ModelState = """{"rate":0.125,"history":[1,true,null],"word":"żaba"}""";
        scenario.Session.Status = "completed";
        scenario.Session.LastActivityAt = Clock.GetCurrentInstant();
        scenario.Session.CompletedAt = Clock.GetCurrentInstant() + Duration.FromMinutes(1);
        scenario.Item.State = "answered";
        scenario.Item.AnswerPayloadHash = new string('a', 64);
        scenario.Item.AnsweredAt = Clock.GetCurrentInstant();
        var answer = NewAnswer(scenario);
        answer.DueOn = word.DueOn;
        answer.IntervalBefore = 1;
        Db.AnswerEvents.Add(answer);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();

        var storedWord = await Db.LearnerWords.SingleAsync();
        storedWord.Should().BeEquivalentTo(word, options => options.Excluding(value => value.ModelState));
        JsonNode.DeepEquals(JsonNode.Parse(storedWord.ModelState), JsonNode.Parse(word.ModelState))
            .Should().BeTrue();
        storedWord.CreatedAt.Should().NotBe(default(Instant));
        storedWord.Version.Should().NotBe(0);
        var session = await Db.LearningSessions.SingleAsync();
        session.Should().BeEquivalentTo(scenario.Session);
        session.CreatedAt.Should().NotBe(default(Instant));
        session.Version.Should().NotBe(0);
        var item = await Db.SessionItems.SingleAsync();
        item.Should().BeEquivalentTo(scenario.Item, options => options.Excluding(value => value.Session));
        item.CreatedAt.Should().NotBe(default(Instant));
        item.Version.Should().NotBe(0);
        var storedAnswer = await Db.AnswerEvents.SingleAsync();
        storedAnswer.Should().BeEquivalentTo(answer, options => options
            .Excluding(value => value.SessionItem).Excluding(value => value.StateBefore));
        JsonNode.DeepEquals(JsonNode.Parse(storedAnswer.StateBefore), JsonNode.Parse(word.ModelState))
            .Should().BeTrue();
        storedAnswer.CreatedAt.Should().NotBe(default(Instant));
        new[] { word.Id, session.Id, item.Id, answer.Id }.Should().OnlyContain(id => id.Version == 7);
    }

    [DockerFact]
    public async Task LearnerWords_RejectDuplicateEntryForLearner()
    {
        var scenario = await SaveScenarioAsync();
        Db.LearnerWords.Add(NewWord(scenario.Learner.Id, scenario.Entry.Id));

        await AssertUniqueViolationAsync("ux_learner_words_learner_entry");
    }

    [DockerFact]
    public async Task Sessions_AllowCompletedHistoryButRejectSecondActiveSession()
    {
        var scenario = await SaveScenarioAsync();
        Db.LearningSessions.AddRange(
            NewSession(scenario.Learner.Id, "completed"), NewSession(scenario.Learner.Id, "incomplete"));
        await Db.SaveChangesAsync();
        (await Db.LearningSessions.CountAsync()).Should().Be(3);
        Db.LearningSessions.Add(NewSession(scenario.Learner.Id));

        await AssertUniqueViolationAsync("ux_learning_sessions_one_active_lang");
    }

    [DockerFact]
    public async Task SessionItems_RejectDuplicatePositionWithinSession()
    {
        var scenario = await SaveScenarioAsync();
        Db.SessionItems.Add(new SessionItem
        {
            SessionId = scenario.Session.Id,
            EntryId = scenario.Entry.Id,
            Position = scenario.Item.Position,
            Kind = "review",
            State = "pending"
        });

        await AssertUniqueViolationAsync("ux_session_items_session_position");
    }

    [DockerFact]
    public async Task AnswerEvents_RejectSecondAnswerForSessionItem()
    {
        var scenario = await SaveScenarioAsync();
        Db.AnswerEvents.Add(NewAnswer(scenario));
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        Db.AnswerEvents.Add(NewAnswer(scenario));

        await AssertUniqueViolationAsync("ux_answer_events_session_item");
    }

    [DockerFact]
    public async Task AnswerEvents_AllowMultipleNullSessionItemsAndNullableAnswerFields()
    {
        var scenario = await SaveScenarioAsync();
        var answer = NewAnswer(scenario);
        answer.SessionItemId = null;
        answer.RawAnswer = null;
        answer.NormalizedAnswer = null;
        var otherAnswer = NewAnswer(scenario);
        otherAnswer.SessionItemId = null;
        Db.AnswerEvents.AddRange(answer, otherAnswer);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();

        var stored = await Db.AnswerEvents.SingleAsync(value => value.Id == answer.Id);
        stored.SessionItemId.Should().BeNull();
        stored.RawAnswer.Should().BeNull();
        stored.NormalizedAnswer.Should().BeNull();
        stored.DueOn.Should().BeNull();
        stored.IntervalBefore.Should().BeNull();
        (await Db.AnswerEvents.CountAsync()).Should().Be(2);
    }

    [DockerFact]
    public async Task Deletion_CascadesSessionItemsAndPreservesAnswerUntilLearnerDeletion()
    {
        var scenario = await SaveScenarioAsync();
        var answer = NewAnswer(scenario);
        Db.AnswerEvents.Add(answer);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();

        await Db.LearningSessions.ExecuteDeleteAsync();
        (await Db.SessionItems.CountAsync()).Should().Be(0);
        var survivingAnswer = await Db.AnswerEvents.SingleAsync();
        survivingAnswer.Id.Should().Be(answer.Id);
        survivingAnswer.SessionItemId.Should().BeNull();
        survivingAnswer.StateBefore.Should().NotBeEmpty();
        (await Db.LearnerWords.CountAsync()).Should().Be(1);

        await Db.Learners.ExecuteDeleteAsync();
        (await Db.LearnerWords.CountAsync()).Should().Be(0);
        (await Db.AnswerEvents.CountAsync()).Should().Be(0);
        (await Db.DictionaryEntries.CountAsync()).Should().Be(1);
        (await Db.EntryVersions.CountAsync()).Should().Be(1);
    }

    [DockerFact]
    public async Task ForeignKeys_PreventDeletingReferencedContent()
    {
        var scenario = await SaveScenarioAsync();
        Db.AnswerEvents.Add(NewAnswer(scenario));
        await Db.SaveChangesAsync();

        var deleteEntry = () => Db.DictionaryEntries.ExecuteDeleteAsync();
        var entryException = await deleteEntry.Should().ThrowAsync<PostgresException>();
        entryException.Which.SqlState.Should().Be(PostgresErrorCodes.ForeignKeyViolation);
        var deleteVersion = () => Db.EntryVersions.ExecuteDeleteAsync();
        var versionException = await deleteVersion.Should().ThrowAsync<PostgresException>();
        versionException.Which.SqlState.Should().Be(PostgresErrorCodes.ForeignKeyViolation);
        versionException.Which.ConstraintName.Should().Be("answer_events_version_id_fkey");
    }

    [DockerFact]
    public async Task LearnerWord_XminRejectsStaleProjectionUpdate()
    {
        var scenario = await SaveScenarioAsync();
        await using (var provider = _postgres.CreateServices(Clock))
        {
            await using var scope = provider.CreateAsyncScope();
            var otherDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var current = await otherDb.LearnerWords.SingleAsync();
            current.Lapses = 1;
            await otherDb.SaveChangesAsync();
        }

        scenario.Word.Lapses = 2;
        var action = () => Db.SaveChangesAsync();
        await action.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [DockerFact]
    public async Task LearningSession_XminRejectsStaleCompletion()
    {
        var scenario = await SaveScenarioAsync();
        await using (var provider = _postgres.CreateServices(Clock))
        {
            await using var scope = provider.CreateAsyncScope();
            var otherDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var current = await otherDb.LearningSessions.SingleAsync();
            current.Status = "incomplete";
            await otherDb.SaveChangesAsync();
        }

        scenario.Session.Status = "completed";
        var action = () => Db.SaveChangesAsync();
        await action.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [DockerFact]
    public async Task SessionItem_XminRejectsStaleAnswer()
    {
        var scenario = await SaveScenarioAsync();
        await using (var provider = _postgres.CreateServices(Clock))
        {
            await using var scope = provider.CreateAsyncScope();
            var otherDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var current = await otherDb.SessionItems.SingleAsync();
            current.State = "answered";
            await otherDb.SaveChangesAsync();
        }

        scenario.Item.State = "skipped";
        var action = () => Db.SaveChangesAsync();
        await action.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    private async Task<Scenario> SaveScenarioAsync()
    {
        var learnerData = LearnerFixtures.NewLearner(clock: Clock);
        var entryData = ContentFixtures.NewPublishedEntry();
        var sessionData = LearningFixtures.NewActiveSession(learnerData, [entryData], Clock);
        var learner = new Learner
        {
            Id = learnerData.Id,
            TelegramUserId = learnerData.TelegramUserId,
            FirstName = learnerData.FirstName,
            TimeZone = learnerData.TimeZone,
            TimeZoneConfirmed = learnerData.TimeZoneConfirmed,
            DailyNewWordsLimit = checked((short)learnerData.DailyNewWordsLimit),
            CanMessage = true
        };
        var entry = new DictionaryEntry
        {
            Id = entryData.Id, ExternalId = entryData.Id.ToString("N"), Domain = "everyday"
        };
        var version = new EntryVersion
        {
            Id = entryData.Version.Id,
            Entry = entry,
            LemmaPl = entryData.Version.LemmaPl,
            PartOfSpeech = "noun",
            PronunciationUk = "жаба",
            ExamplePl = "To jest żaba.",
            ExampleUk = "Це жаба.",
            ValidationStatus = entryData.Version.ValidationStatus,
            CreatedByKind = "import",
            ContentHash = new string('a', 64)
        };
        Db.AddRange(learner, version);
        await Db.SaveChangesAsync();
        var word = NewWord(learner.Id, entry.Id);
        var session = new LearningSession
        {
            Id = sessionData.Id,
            LearnerId = learner.Id,
            LearnerDay = sessionData.LearnerDay,
            EndsAt = sessionData.EndsAt,
            Status = sessionData.Status,
            Kind = "plan"
        };
        var itemData = sessionData.Items.Single();
        var item = new SessionItem
        {
            Id = itemData.Id,
            Session = session,
            EntryId = itemData.EntryId,
            Position = checked((short)itemData.Position),
            Kind = itemData.Kind,
            State = itemData.State
        };
        Db.AddRange(word, item);
        await Db.SaveChangesAsync();
        return new Scenario(learner, entry, version, word, session, item);
    }

    private static LearnerWord NewWord(Guid learnerId, Guid entryId) => new()
    {
        LearnerId = learnerId,
        EntryId = entryId,
        Status = "review",
        ModelState = "{}",
        SchedulerVersion = "memorize-v1"
    };

    private LearningSession NewSession(Guid learnerId, string status = "active") => new()
    {
        LearnerId = learnerId,
        LearnerDay = new LocalDate(2026, 10, 31),
        EndsAt = Clock.GetCurrentInstant() + Duration.FromHours(14),
        Status = status,
        Kind = "plan"
    };

    private AnswerEvent NewAnswer(Scenario scenario) => new()
    {
        LearnerId = scenario.Learner.Id,
        EntryId = scenario.Entry.Id,
        VersionId = scenario.ContentVersion.Id,
        SessionItemId = scenario.Item.Id,
        ExerciseKind = "review",
        Outcome = "correct",
        RawAnswer = " Żaba ",
        NormalizedAnswer = "żaba",
        AnsweredAt = Clock.GetCurrentInstant(),
        LearnerDay = scenario.Session.LearnerDay,
        TimeZone = scenario.Learner.TimeZone,
        SchedulerVersion = scenario.Word.SchedulerVersion,
        StateBefore = scenario.Word.ModelState
    };

    private async Task AssertUniqueViolationAsync(string constraint)
    {
        var action = () => Db.SaveChangesAsync();
        var exception = await action.Should().ThrowAsync<DbUpdateException>();
        var postgres = exception.Which.InnerException.Should().BeOfType<PostgresException>().Which;
        postgres.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
        postgres.ConstraintName.Should().Be(constraint);
    }

    private static Task<List<string>> ReadRelationsAsync(AppDbContext db) => db.Database.SqlQueryRaw<string>("""
        SELECT relname AS "Value" FROM pg_class
        WHERE relnamespace = 'public'::regnamespace
          AND relname <> '__EFMigrationsHistory'
          AND oid NOT IN (
              SELECT indexrelid FROM pg_index WHERE indrelid = '"__EFMigrationsHistory"'::regclass)
        """).ToListAsync();

    private sealed record Scenario(Learner Learner, DictionaryEntry Entry, EntryVersion ContentVersion,
        LearnerWord Word, LearningSession Session, SessionItem Item);
}
