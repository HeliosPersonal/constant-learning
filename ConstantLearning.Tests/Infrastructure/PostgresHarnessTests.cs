using ConstantLearning.Data.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Npgsql;

namespace ConstantLearning.Tests.Infrastructure;

public sealed class PostgresHarnessTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;

    [DockerFact]
    public async Task Container_AppliesMigrationsAndPersistsDataWithInjectedClock()
    {
        (await Db.Database.GetAppliedMigrationsAsync()).Should().NotBeEmpty();
        (await Db.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        await using var connection = new NpgsqlConnection(_postgres.ConnectionString);
        await connection.OpenAsync();
        connection.PostgreSqlVersion.Major.Should().Be(16);
        Services.GetRequiredService<IClock>().Should().BeSameAs(Clock);
        Clock.Advance(Duration.FromMinutes(5));

        Db.Words.Add(new Word
        {
            TargetWord = "żaba",
            SourceMeaning = "жаба",
            PhoneticTranscription = "жаба",
            FrequencyRank = 1,
            ImportedAt = Clock.GetCurrentInstant().ToDateTimeUtc()
        });
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();

        var word = await Db.Words.SingleAsync();
        word.TargetWord.Should().Be("żaba");
        word.ImportedAt.Should().Be(Clock.GetCurrentInstant().ToDateTimeUtc());
    }

    [DockerFact]
    public async Task Reset_RemovesMutableDataAndPreservesMigrationHistory()
    {
        Db.Words.Add(new Word
        {
            TargetWord = "kot",
            SourceMeaning = "кіт",
            PhoneticTranscription = "кот",
            ImportedAt = Clock.GetCurrentInstant().ToDateTimeUtc()
        });
        await Db.SaveChangesAsync();

        await _postgres.ResetAsync();
        Db.ChangeTracker.Clear();

        (await Db.Words.CountAsync()).Should().Be(0);
        (await Db.Database.GetAppliedMigrationsAsync()).Should().NotBeEmpty();
    }
}
