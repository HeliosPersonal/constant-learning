using ConstantLearning.Features.Learning.Domain.Evaluation;

namespace ConstantLearning.Tests.Learning.Domain.Evaluation;

public class SpellingDiffTests
{
    [Fact]
    public void Create_MatchesDiacriticExampleFromApiContract()
    {
        var diff = SpellingDiff.Create("żaba", "zaba");

        Assert.Equal([
            new(DiffOperation.Substitute, "ż", "z"),
            new DiffSegment(DiffOperation.Equal, "aba", "aba")
        ], diff);
    }

    [Fact]
    public void Create_InsertsMissingExpectedLetters()
    {
        var diff = SpellingDiff.Create("kot", "ot");

        Assert.Equal([
            new(DiffOperation.Insert, "k", ""),
            new DiffSegment(DiffOperation.Equal, "ot", "ot")
        ], diff);
    }

    [Fact]
    public void Create_DeletesExtraActualLetters()
    {
        var diff = SpellingDiff.Create("ot", "kot");

        Assert.Equal([
            new(DiffOperation.Delete, "", "k"),
            new DiffSegment(DiffOperation.Equal, "ot", "ot")
        ], diff);
    }

    [Theory]
    [InlineData("", "", null)]
    [InlineData("", "abc", DiffOperation.Delete)]
    [InlineData("abc", "", DiffOperation.Insert)]
    [InlineData("abc", "abc", DiffOperation.Equal)]
    [InlineData("ab", "ba", DiffOperation.Substitute)]
    public void Create_MergesAdjacentOperationsAndBreaksTiesDeterministically(
        string expected, string actual, DiffOperation? operation)
    {
        var diff = SpellingDiff.Create(expected, actual);

        if (operation is null)
        {
            Assert.Empty(diff);
        }
        else
        {
            Assert.Equal(new(operation.Value, expected, actual), Assert.Single(diff));
        }
    }

    [Theory]
    [InlineData("a\u0301", "a")]
    [InlineData("🧑‍💻", "🧑")]
    [InlineData("𐐨", "𐐩")]
    public void Create_DoesNotSplitCombiningSequencesOrSurrogatePairs(string expected, string actual)
    {
        var diff = SpellingDiff.Create(expected, actual);

        Assert.Equal(new(DiffOperation.Substitute, expected, actual), Assert.Single(diff));
    }

    [Theory]
    [InlineData("żaba", "żababa")]
    [InlineData("dzień dobry", "dzien bardzo dobry")]
    [InlineData("łódź", "odz")]
    [InlineData("aba", "bab")]
    public void Create_PreservesBothTextsAndReturnsStableSegments(string expected, string actual)
    {
        var diff = SpellingDiff.Create(expected, actual);

        Assert.Equal(expected, string.Concat(diff.Select(segment => segment.Expected)));
        Assert.Equal(actual, string.Concat(diff.Select(segment => segment.Actual)));
        Assert.Equal(diff, SpellingDiff.Create(expected, actual));
        Assert.All(diff.Zip(diff.Skip(1)), pair => Assert.NotEqual(pair.First.Op, pair.Second.Op));
    }
}
