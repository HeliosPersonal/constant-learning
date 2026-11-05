using ConstantLearning.Features.Content.Search.Forms;
using AwesomeAssertions;

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

    [Theory]
    // Німецьке розкриття умлаутів/ескцет (DIN 5007-2): ä→ae, ö→oe, ü→ue, ß→ss.
    [InlineData("Fußball", "fussball")]
    [InlineData("STRASSE", "strasse")]
    [InlineData("Mädchen", "maedchen")]
    [InlineData("schön", "schoen")]
    [InlineData("über", "ueber")]
    [InlineData("GRÜßEN", "gruessen")]
    // Капіталізація іменника згортається загальним lowercase — окремого правила не треба.
    [InlineData("Tisch", "tisch")]
    // Складені слова індексуються цілими (без декомпаунда), включно з внутрішнім ß/умлаутом.
    [InlineData("Fußballstraße", "fussballstrasse")]
    [InlineData("Donaudampfschiff", "donaudampfschiff")]
    public void Fold_ExpandsGermanUmlautsAndEszett(string input, string expected)
        => SearchTextNormalizer.Fold(input).Should().Be(expected);

    [Theory]
    // Нідерландська трема знімається загальним NFD (не розкривається): ë→e, ï→i.
    [InlineData("drieën", "drieen")]
    [InlineData("geïnteresseerd", "geinteresseerd")]
    [InlineData("Huis", "huis")]
    public void Fold_DropsDutchTremaViaGenericNfd(string input, string expected)
        => SearchTextNormalizer.Fold(input).Should().Be(expected);

    [Fact]
    public void Fold_IsSymmetricForGermanUmlautlessTyping()
        => SearchTextNormalizer.Fold("Fußball").Should().Be(SearchTextNormalizer.Fold("fussball"));

    [Fact]
    public void Normalize_PreservesDiacriticsForLexiconLookup()
        => SearchTextNormalizer.Normalize("  ŻABA ").Should().Be("żaba");
}
