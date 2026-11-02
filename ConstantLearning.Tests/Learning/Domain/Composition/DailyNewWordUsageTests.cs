using ConstantLearning.Features.Learning.Domain.Composition;

namespace ConstantLearning.Tests.Learning.Domain.Composition;

public class DailyNewWordUsageTests
{
    [Theory]
    [InlineData(3, 0, 0, 3)]
    [InlineData(3, 2, 0, 1)]
    [InlineData(3, 3, 0, 0)]
    [InlineData(3, 5, 0, 0)]
    [InlineData(3, 9, 9, 3)]
    [InlineData(3, 10, 10, 3)]
    [InlineData(3, 11, 11, 2)]
    [InlineData(3, 12, 11, 1)]
    [InlineData(3, 13, 11, 0)]
    [InlineData(3, 20, 20, 0)]
    [InlineData(0, 0, 0, 0)]
    public void Ac06_AlreadyKnownExemptionIsLimitedToFirstTenWords(
        int limit, int shown, int known, int expectedRemaining)
    {
        Assert.Equal(expectedRemaining, new DailyNewWordUsage(shown, known).Remaining(limit));
    }

    [Fact]
    public void AlreadyKnownBeyondTenConsumesPlacesInFollowingSession()
    {
        var course = Enumerable.Range(1, 20).Select(id =>
            new CourseWordCandidate(new Guid(id, 0, 0, new byte[8]), DomainQuota.Everyday, id, id, true, false));

        var result = SessionComposer.Compose(new(2026, 11, 1), [], course, 3, new(12, 11));

        Assert.Single(result.Words);
        Assert.Equal(1, result.DailyNewWordsRemaining);
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(3, -1, 0)]
    [InlineData(3, 0, -1)]
    public void NegativeLimitsAndCountersAreRejected(int limit, int shown, int known)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DailyNewWordUsage(shown, known).Remaining(limit));
    }

    [Fact]
    public void AlreadyKnownCountCannotExceedShownCount()
    {
        Assert.Throws<ArgumentException>(() => new DailyNewWordUsage(2, 3).Remaining(5));
    }
}
