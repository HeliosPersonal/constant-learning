using ConstantLearning.Features.Learning.Domain.Evaluation;

namespace ConstantLearning.Tests.Learning.Domain.Evaluation;

public class AnswerEvaluatorTests
{
    private static readonly EvaluationEntry Frog = new(
        Guid.Parse("0192f0a0-0000-7000-8000-000000000001"), "żaba", "жаба", true);

    [Theory]
    [InlineData("żaba")]
    [InlineData("  ŻABA  ")]
    [InlineData("ŻАBА, . ! ? ; : …")]
    [InlineData("z\u0307aba")]
    public void Ac12_AcceptsCanonicalFormAfterNormalization(string answer)
    {
        var result = AnswerEvaluator.Evaluate(answer, false, Frog);

        Assert.Equal(AnswerOutcome.Correct, result.Outcome);
        Assert.True(result.IsEvaluated);
        Assert.False(result.IsMistake);
        Assert.False(result.IsNeutral);
        Assert.False(result.DisputeAllowed);
        Assert.Null(result.ErrorCode);
        Assert.Equal("żaba", result.NormalizedAnswer);
        Assert.Equal("żaba", result.CorrectForm);
        Assert.Empty(result.Diff);
    }

    [Fact]
    public void Ac12_AcceptsApprovedVariantsUsingTheSameNormalization()
    {
        var entry = Frog with { ApprovedPolishVariants = ["Rорucha!"] };

        var result = AnswerEvaluator.Evaluate("  ROPUCHA . ", false, entry);

        Assert.Equal(AnswerOutcome.Correct, result.Outcome);
        Assert.Equal("żaba", result.CorrectForm);
    }

    [Fact]
    public void Ac12_CollapsesUnicodeWhitespaceInMultiwordForms()
    {
        var entry = Frog with { PolishLemma = "dzień dobry" };

        var result = AnswerEvaluator.Evaluate(" DZIEŃ\u00a0\t\nDOBRY ", false, entry);

        Assert.Equal(AnswerOutcome.Correct, result.Outcome);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\n\u00a0 ")]
    [InlineData(" . , ! ? ; : … ")]
    public void Ac13_DoesNotEvaluateEmptyNormalizedAnswer(string? answer)
    {
        var result = AnswerEvaluator.Evaluate(answer, false, Frog);

        Assert.Null(result.Outcome);
        Assert.Equal("answer.empty", result.ErrorCode);
        Assert.False(result.IsEvaluated);
        Assert.False(result.IsMistake);
        Assert.False(result.DisputeAllowed);
        Assert.Empty(result.Diff);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("żaba")]
    [InlineData("жаба")]
    public void Ac14_DontKnowIsMistakeRegardlessOfTextLeftInInput(string? answer)
    {
        var result = AnswerEvaluator.Evaluate(answer, true, Frog);

        Assert.Equal(AnswerOutcome.DontKnow, result.Outcome);
        Assert.True(result.IsEvaluated);
        Assert.True(result.IsMistake);
        Assert.False(result.DisputeAllowed);
        Assert.Null(result.ErrorCode);
        Assert.Equal("żaba", result.CorrectForm);
        Assert.Empty(result.Diff);
    }

    [Theory]
    [InlineData("żaba", "zaba")]
    [InlineData("żaba", "źaba")]
    [InlineData("zaba", "żaba")]
    [InlineData("kąt", "kat")]
    [InlineData("być", "byc")]
    [InlineData("gęś", "ges")]
    [InlineData("łódź", "lodz")]
    [InlineData("koń", "kon")]
    [InlineData("źle", "zle")]
    public void Ac15_DiacriticDifferenceIsMistakeWithoutDispute(string expected, string answer)
    {
        var result = AnswerEvaluator.Evaluate(answer, false, Frog with { PolishLemma = expected });

        Assert.Equal(AnswerOutcome.MistakeDiacritic, result.Outcome);
        Assert.True(result.IsMistake);
        Assert.False(result.DisputeAllowed);
        Assert.Equal(expected, result.CorrectForm);
        Assert.Equal(expected, string.Concat(result.Diff.Select(segment => segment.Expected)));
        Assert.Equal(answer, string.Concat(result.Diff.Select(segment => segment.Actual)));
    }

    [Fact]
    public void Ac15_DiacriticDifferenceCanReferToApprovedVariant()
    {
        var entry = Frog with { ApprovedPolishVariants = ["ropuszka", "żabka"] };

        var result = AnswerEvaluator.Evaluate("zabka", false, entry);

        Assert.Equal(AnswerOutcome.MistakeDiacritic, result.Outcome);
        Assert.Equal("żabka", result.CorrectForm);
        Assert.Equal("żabka", string.Concat(result.Diff.Select(segment => segment.Expected)));
    }

    [Fact]
    public void Ac15_ExactVariantTakesPriorityOverDiacriticDifference()
    {
        var entry = Frog with { ApprovedPolishVariants = ["zaba"] };

        Assert.Equal(AnswerOutcome.Correct, AnswerEvaluator.Evaluate("zaba", false, entry).Outcome);
    }

    [Theory]
    [InlineData("żapa")]
    [InlineData("żab")]
    [InlineData("żabaa")]
    [InlineData("żába")]
    [InlineData("kot")]
    public void Ac16_OtherDifferenceIsMistakeWithCanonicalDiffAndDispute(string answer)
    {
        var result = AnswerEvaluator.Evaluate(answer, false, Frog);

        Assert.Equal(AnswerOutcome.MistakeTypo, result.Outcome);
        Assert.True(result.IsMistake);
        Assert.True(result.DisputeAllowed);
        Assert.Null(result.ErrorCode);
        Assert.Equal("żaba", result.CorrectForm);
        Assert.Equal("żaba", string.Concat(result.Diff.Select(segment => segment.Expected)));
        Assert.Equal(answer, string.Concat(result.Diff.Select(segment => segment.Actual)));
    }

    [Fact]
    public void Ac16_DiffUsesNormalizedText()
    {
        var result = AnswerEvaluator.Evaluate("  ŻAPA ! ", false, Frog);

        Assert.Equal("żapa", result.NormalizedAnswer);
        Assert.Equal("żapa", string.Concat(result.Diff.Select(segment => segment.Actual)));
    }

    [Fact]
    public void Ac56_ApprovedOtherLemmaWithSameCanonicalTranslationIsNeutral()
    {
        var other = OtherEntry("ropucha");

        var result = AnswerEvaluator.Evaluate("  ROPUCHA! ", false, Frog, [other]);

        Assert.Equal(AnswerOutcome.Synonym, result.Outcome);
        Assert.True(result.IsEvaluated);
        Assert.True(result.IsNeutral);
        Assert.False(result.IsMistake);
        Assert.False(result.DisputeAllowed);
        Assert.Null(result.ErrorCode);
        Assert.Equal("żaba", result.CorrectForm);
        Assert.Empty(result.Diff);
    }

    [Fact]
    public void Ac56_CurrentEntryDoesNotCountAsAnotherSynonymEntry()
    {
        var sameEntry = Frog with { PolishLemma = "ropucha" };

        Assert.Equal(AnswerOutcome.MistakeTypo,
            AnswerEvaluator.Evaluate("ropucha", false, Frog, [sameEntry]).Outcome);
    }

    [Fact]
    public void Ac56_NonApprovedEntryDoesNotCountAsSynonym()
    {
        var unapproved = OtherEntry("ropucha") with { IsApproved = false };

        Assert.Equal(AnswerOutcome.MistakeTypo,
            AnswerEvaluator.Evaluate("ropucha", false, Frog, [unapproved]).Outcome);
    }

    [Theory]
    [InlineData("ропуха")]
    [InlineData("Жаба")]
    [InlineData("жаба ")]
    public void Ac56_RequiresExactCanonicalUkrainianTranslation(string translation)
    {
        var differentTranslation = OtherEntry("ropucha") with { UkrainianTranslation = translation };

        Assert.Equal(AnswerOutcome.MistakeTypo,
            AnswerEvaluator.Evaluate("ropucha", false, Frog, [differentTranslation]).Outcome);
    }

    [Fact]
    public void Ac56_DoesNotAcceptOtherEntriesVariantsAsSynonyms()
    {
        var other = OtherEntry("ropucha") with { ApprovedPolishVariants = ["ropuszka"] };

        Assert.Equal(AnswerOutcome.MistakeTypo,
            AnswerEvaluator.Evaluate("ropuszka", false, Frog, [other]).Outcome);
    }

    [Fact]
    public void Ac56_AcceptedCurrentVariantTakesPriorityOverSynonym()
    {
        var entry = Frog with { ApprovedPolishVariants = ["ropucha"] };

        Assert.Equal(AnswerOutcome.Correct,
            AnswerEvaluator.Evaluate("ropucha", false, entry, [OtherEntry("ropucha")]).Outcome);
    }

    [Fact]
    public void Ac56_SynonymTakesPriorityOverDiacriticMistake()
    {
        var result = AnswerEvaluator.Evaluate("zaba", false, Frog, [OtherEntry("zaba")]);

        Assert.Equal(AnswerOutcome.Synonym, result.Outcome);
    }

    [Theory]
    [InlineData("жаба")]
    [InlineData("ŻABЫ")]
    [InlineData("ґ")]
    public void Ac57_RemainingCyrillicLettersAreNotEvaluated(string answer)
    {
        var result = AnswerEvaluator.Evaluate(answer, false, Frog);

        Assert.Equal("answer.wrong_layout", result.ErrorCode);
        Assert.Null(result.Outcome);
        Assert.False(result.IsEvaluated);
        Assert.False(result.IsMistake);
        Assert.False(result.DisputeAllowed);
        Assert.Empty(result.Diff);
    }

    [Fact]
    public void Ac57_WrongLayoutIsRejectedBeforeAcceptedFormMatching()
    {
        var entry = Frog with { ApprovedPolishVariants = ["жаба"] };

        Assert.Equal("answer.wrong_layout", AnswerEvaluator.Evaluate("жаба", false, entry).ErrorCode);
    }

    [Fact]
    public void Evaluate_RepeatedCallsProduceIdenticalStructuredDiff()
    {
        var first = AnswerEvaluator.Evaluate("żababa", false, Frog);

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var next = AnswerEvaluator.Evaluate("żababa", false, Frog);
            Assert.Equal(first.Outcome, next.Outcome);
            Assert.Equal(first.CorrectForm, next.CorrectForm);
            Assert.Equal(first.Diff, next.Diff);
        }
    }

    private static EvaluationEntry OtherEntry(string lemma) => new(
        Guid.Parse("0192f0a0-0000-7000-8000-000000000002"), lemma, Frog.UkrainianTranslation, true);
}
