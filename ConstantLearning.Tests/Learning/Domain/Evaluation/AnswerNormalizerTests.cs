using ConstantLearning.Features.Learning.Domain.Evaluation;

namespace ConstantLearning.Tests.Learning.Domain.Evaluation;

public class AnswerNormalizerTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData(" \t\r\n\u00a0\u2003 ", "")]
    [InlineData("  DZIEŃ\t\n DOBRY  ", "dzień dobry")]
    [InlineData("żaba.", "żaba")]
    [InlineData("żaba,", "żaba")]
    [InlineData("żaba!", "żaba")]
    [InlineData("żaba?", "żaba")]
    [InlineData("żaba;", "żaba")]
    [InlineData("żaba:", "żaba")]
    [InlineData("żaba…", "żaba")]
    [InlineData(" ŻABA . , ! ? ; : … \t", "żaba")]
    [InlineData(" . , ! ? ; : … ", "")]
    [InlineData("biało-czerwony", "biało-czerwony")]
    [InlineData("coś, co lubię", "coś, co lubię")]
    [InlineData(".żaba", ".żaba")]
    [InlineData("żaba-", "żaba-")]
    [InlineData("żaba\"", "żaba\"")]
    [InlineData("a\u0328c\u0301e\u0328łn\u0301o\u0301s\u0301z\u0301z\u0307", "ąćęłńóśźż")]
    [InlineData("а", "a")]
    [InlineData("е", "e")]
    [InlineData("і", "i")]
    [InlineData("о", "o")]
    [InlineData("р", "p")]
    [InlineData("с", "c")]
    [InlineData("у", "y")]
    [InlineData("х", "x")]
    [InlineData("АЕІОРСУХ", "aeiopcyx")]
    [InlineData("ŻАBА", "żaba")]
    [InlineData("żába", "żába")]
    public void Normalize_AppliesOnlySpecifiedRules(string? answer, string expected)
    {
        var normalized = AnswerNormalizer.Normalize(answer);

        Assert.Equal(expected, normalized);
        Assert.Equal(normalized, AnswerNormalizer.Normalize(normalized));
    }

    [Theory]
    [InlineData("ąćęłńóśźż", "acelnoszz")]
    [InlineData("żaba", "zaba")]
    [InlineData("źaba", "zaba")]
    [InlineData("żába", "zába")]
    [InlineData("a\u0301", "a\u0301")]
    public void FoldPolishDiacritics_DoesNotStripUnspecifiedAccents(string answer, string expected)
    {
        Assert.Equal(expected, AnswerNormalizer.FoldPolishDiacritics(answer));
    }

    [Theory]
    [InlineData("жаба", true)]
    [InlineData("żabы", true)]
    [InlineData("ґ", true)]
    [InlineData("ѣ", true)]
    [InlineData("\u1c80", true)]
    [InlineData("\ua641", true)]
    [InlineData("\U0001e030", true)]
    [InlineData("żaba", false)]
    [InlineData("а е і о р с у х", false)]
    [InlineData("α", false)]
    public void ContainsCyrillicLetters_ChecksRemainingLettersAfterNormalization(string answer, bool expected)
    {
        Assert.Equal(expected, AnswerNormalizer.ContainsCyrillicLetters(AnswerNormalizer.Normalize(answer)));
    }
}
