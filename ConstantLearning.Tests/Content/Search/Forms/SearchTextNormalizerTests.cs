using ConstantLearning.Features.Content.Search.Forms;
using FluentAssertions;

namespace ConstantLearning.Tests.Content.Search.Forms;

public sealed class SearchTextNormalizerTests
{
    [Theory]
    [InlineData("  ZAŻÓŁĆ GĘŚLĄ JAŹŃ  ", "zazolc gesla jazn")]
    [InlineData("ҐРУНТ ЇЖА ЙОД Єнот", "грунт іжа иод єнот")]
    [InlineData("z\u0307o\u0301łw", "zolw")]
    [InlineData("Ї\u0301жа", "іжа")]
    public void Fold_IsInvariantAndRemovesPolishAndUkrainianDiacritics(string input, string expected)
        => SearchTextNormalizer.Fold(input).Should().Be(expected);

    [Fact]
    public void Normalize_PreservesDiacriticsForLexiconLookup()
        => SearchTextNormalizer.Normalize("  ŻABA ").Should().Be("żaba");
}
