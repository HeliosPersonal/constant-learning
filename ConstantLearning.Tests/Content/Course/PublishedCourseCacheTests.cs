using ConstantLearning.Features.Content.Course;
using ConstantLearning.Features.Content.Data;
using ConstantLearning.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using ConstantLearning.Platform.Persistence;

namespace ConstantLearning.Tests.Content.Course;

public sealed class PublishedCourseCacheTests : PostgresIntegrationTest
{
    private readonly PostgresFixture _postgres;

    public PublishedCourseCacheTests(PostgresFixture postgres) : base(postgres) => _postgres = postgres;
    [DockerFact]
    public async Task Read_UsesFreshPublicationAndStatusAcrossWarmCache()
    {
        var entry = new DictionaryEntry { ExternalId = "cache-test", Domain = "everyday", CoursePosition = 1 };
        var first = Version(entry, "żaba", 1);
        var second = Version(entry, "kot", 2);
        Db.EntryVersions.AddRange(first, second);
        Db.VersionTranslations.AddRange(
            new VersionTranslation { VersionId = first.Id, Kind = "canonical", TextUk = "жаба", Position = 0 },
            new VersionTranslation { VersionId = first.Id, Kind = "alternative", TextUk = "жабка", Position = 1 },
            new VersionTranslation { VersionId = second.Id, Kind = "canonical", TextUk = "кіт", Position = 0 });
        Db.VersionAcceptedAnswers.Add(new VersionAcceptedAnswer { VersionId = first.Id, TextPl = "żabka", IsCanonical = false });
        await Db.SaveChangesAsync();
        entry.PublishedVersionId = first.Id;
        await Db.SaveChangesAsync();

        await using var provider = new ServiceCollection().AddSingleton(Db).AddPublishedCourseReader().BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IPublishedCourseReader>();
        var original = (await reader.ReadAsync()).Should().ContainSingle().Subject;
        original.LemmaPl.Should().Be("żaba");
        original.TranslationsUk.Should().Equal("жаба", "жабка");
        (await Db.VersionAcceptedAnswers.FindAsync((await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(Db.VersionAcceptedAnswers)).Id))!.TextPl.Should().Be("żabka");

        entry.PublishedVersionId = second.Id;
        entry.CoursePosition = 7;
        await Db.SaveChangesAsync();
        var published = (await reader.ReadAsync()).Should().ContainSingle().Subject;
        published.LemmaPl.Should().Be("kot");
        published.CoursePosition.Should().Be(7);
        published.Rank.Should().Be(2);

        second.ValidationStatus = "needs_review";
        await Db.SaveChangesAsync();
        (await reader.ReadAsync()).Should().BeEmpty();
        second.ValidationStatus = "approved";
        entry.PublishedVersionId = first.Id;
        await Db.SaveChangesAsync();
        var rollback = (await reader.ReadAsync()).Should().ContainSingle().Subject;
        rollback.LemmaPl.Should().Be("żaba");
        rollback.TranslationsUk.Should().Equal("жаба", "жабка");

        entry.PublishedVersionId = null;
        await Db.SaveChangesAsync();
        (await reader.ReadAsync()).Should().BeEmpty();
    }

    [DockerFact]
    public async Task ConcurrentColdAndWarmReads_ReturnSamePublishedPayload()
    {
        var entry = new DictionaryEntry { ExternalId = "concurrent-cache", Domain = "everyday", CoursePosition = 1 };
        var version = Version(entry, "żaba", 1);
        Db.EntryVersions.Add(version);
        Db.VersionTranslations.Add(new VersionTranslation { VersionId = version.Id, Kind = "canonical", TextUk = "жаба", Position = 0 });
        await Db.SaveChangesAsync();
        entry.PublishedVersionId = version.Id;
        await Db.SaveChangesAsync();

        var connection = _postgres.ConnectionString;
        await using var provider = new ServiceCollection()
            .AddDbContext<AppDbContext>(options => options.UseNpgsql(connection, npgsql => npgsql.UseNodaTime()).UseSnakeCaseNamingConvention())
            .AddPublishedCourseReader().BuildServiceProvider();
        async Task ReadInScope()
        {
            await using var scope = provider.CreateAsyncScope();
            var course = await scope.ServiceProvider.GetRequiredService<IPublishedCourseReader>().ReadAsync();
            course.Should().ContainSingle().Which.LemmaPl.Should().Be("żaba");
        }
        await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => ReadInScope()));
        await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => ReadInScope()));
    }

    private static EntryVersion Version(DictionaryEntry entry, string lemma, int rank) => new()
    {
        Entry = entry,
        LemmaPl = lemma,
        PartOfSpeech = "noun",
        PronunciationUk = "слово",
        ExamplePl = "To słowo.",
        ExampleUk = "Це слово.",
        ValidationStatus = "approved",
        CreatedByKind = "import",
        ContentHash = new string('a', 64),
        FrequencyRank = rank
    };
}
