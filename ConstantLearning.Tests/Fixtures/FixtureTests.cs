using ConstantLearning.Tests.Infrastructure;
using AwesomeAssertions;
using NodaTime;
using NodaTime.Testing;

namespace ConstantLearning.Tests.Fixtures;

public sealed class FixtureTests
{
    [Fact]
    public void Factories_CreateRelatedDataWithSyntheticIdentityAndFrozenTime()
    {
        var clock = new FakeClock(Instant.FromUtc(2026, 10, 25, 0, 30));
        var learner = LearnerFixtures.NewLearner("Europe/Warsaw", 3, clock);
        var entry = ContentFixtures.NewPublishedEntry("kot", "кіт", ["kot", "kotek"]);
        var session = LearningFixtures.NewActiveSession(learner, [entry], clock);
        var report = ContentFixtures.NewContentReport(learner, entry, "card", clock: clock);

        learner.FirstName.Should().Be("Test User");
        learner.TelegramUserId.Should().BeGreaterThanOrEqualTo(900_000_000);
        LearnerFixtures.NewLearner().TelegramUserId.Should().NotBe(learner.TelegramUserId);
        learner.CreatedAt.Should().Be(clock.GetCurrentInstant());
        learner.Id.Version.Should().Be(7);
        entry.Version.Id.Should().Be(entry.PublishedVersionId);
        entry.Version.EntryId.Should().Be(entry.Id);
        entry.Version.ValidationStatus.Should().Be("approved");
        entry.Version.AcceptedAnswers.Should().Equal("kot", "kotek");
        session.LearnerId.Should().Be(learner.Id);
        session.LearnerDay.Should().Be(new LocalDate(2026, 10, 25));
        session.EndsAt.Should().Be(Instant.FromUtc(2026, 10, 25, 23, 0));
        session.Items.Single().SessionId.Should().Be(session.Id);
        session.Items.Single().VersionId.Should().Be(entry.PublishedVersionId);
        report.LearnerId.Should().Be(learner.Id);
        report.EntryId.Should().Be(entry.Id);
        report.CreatedAt.Should().Be(clock.GetCurrentInstant());
    }

    [Fact]
    public void Signer_UsesSyntheticIdentityAndFrozenAuthDate()
    {
        var clock = new FakeClock(LearnerFixtures.DefaultInstant);
        var learner = LearnerFixtures.NewLearner(clock: clock);
        var authorization = new TelegramInitDataSigner().Authorization(learner, clock);

        authorization.Scheme.Should().Be("tma");
        authorization.Parameter.Should().Contain("auth_date=1793437200");
        authorization.Parameter.Should().Contain("Test%20User");
    }

    [Fact]
    public void Signer_MatchesAnIndependentHmacVector()
    {
        // Вектор обчислено незалежно через Python hmac/hashlib для тих самих вхідних полів.
        var signed = new TelegramInitDataSigner().Sign(900_000_001, LearnerFixtures.DefaultInstant);
        signed.Should().Contain("hash=a836d9a384609851a5eec7ea545e4bc9c5d3ca2769fa5cd6024610da356e97e0");
    }
}
