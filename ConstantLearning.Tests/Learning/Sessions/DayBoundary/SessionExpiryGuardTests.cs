using ConstantLearning.Features.Learning.Data;
using ConstantLearning.Features.Learning.Sessions.DayBoundary;
using NodaTime;

namespace ConstantLearning.Tests.Learning.Sessions.DayBoundary;

public sealed class SessionExpiryGuardTests
{
    private static readonly Instant Boundary = Instant.FromUtc(2026, 10, 31, 23, 0);

    [Fact]
    public void BeforeImmutableBoundary_DoesNotChangeSession()
    {
        var session = Session("active");
        Assert.Null(SessionExpiryGuard.CheckAndClose(session, Boundary - Duration.FromNanoseconds(1)));
        Assert.Equal("active", session.Status);
        Assert.Equal(Boundary, session.EndsAt);
        Assert.Equal(Boundary - Duration.FromHours(1), session.LastActivityAt);
        Assert.Null(session.CompletedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(86400)]
    public void AtOrAfterBoundary_ClosesActiveWithoutCountingCompletion(int seconds)
    {
        var session = Session("active");
        var result = SessionExpiryGuard.CheckAndClose(session, Boundary + Duration.FromSeconds(seconds));
        Assert.Equal("session.expired", result!.Code);
        Assert.Equal("incomplete", session.Status);
        Assert.Null(session.CompletedAt);
        Assert.Equal(Boundary, session.EndsAt);
        Assert.Equal(new LocalDate(2026, 10, 31), session.LearnerDay);
        Assert.Equal(Boundary - Duration.FromHours(1), session.LastActivityAt);
        Assert.Equal(result, SessionExpiryGuard.CheckAndClose(session, Boundary + Duration.FromDays(1)));
    }

    [Theory]
    [InlineData("completed")]
    [InlineData("incomplete")]
    public void ExpiredOldActionIsRejected_WithoutRewritingFinishedSession(string status)
    {
        var session = Session(status);
        var completedAt = status == "completed" ? Boundary - Duration.FromHours(1) : (Instant?)null;
        session.CompletedAt = completedAt;
        Assert.Equal("session.expired", SessionExpiryGuard.CheckAndClose(session, Boundary)!.Code);
        Assert.Equal(status, session.Status);
        Assert.Equal(completedAt, session.CompletedAt);
    }

    private static LearningSession Session(string status) => new()
    {
        LearnerId = Guid.NewGuid(),
        LearnerDay = new LocalDate(2026, 10, 31),
        EndsAt = Boundary,
        Status = status,
        Kind = "plan",
        LastActivityAt = Boundary - Duration.FromHours(1),
        CreatedAt = Boundary - Duration.FromHours(2)
    };
}
