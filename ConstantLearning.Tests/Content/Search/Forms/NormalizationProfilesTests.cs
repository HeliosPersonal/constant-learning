using ConstantLearning.Features.Content.Search.Forms;
using FluentAssertions;

namespace ConstantLearning.Tests.Content.Search.Forms;

public sealed class NormalizationProfilesTests
{
    [Theory]
    // Німецький рід: провідний артикль der/die/das відкидається, лишається голий іменник.
    [InlineData("der Hund", "Hund")]
    [InlineData("die Katze", "Katze")]
    [InlineData("das Haus", "Haus")]
    // Регістро-незалежно (словникова форма може починатися з великої літери артикля).
    [InlineData("Der Hund", "Hund")]
    public void German_StripsLeadingGenderArticle(string display, string expected)
        => NormalizationProfiles.German.StripLeadingArticle(display).Should().Be(expected);

    [Theory]
    // Нідерландські артиклі de/het.
    [InlineData("de man", "man")]
    [InlineData("het huis", "huis")]
    [InlineData("de deur", "deur")]
    public void Dutch_StripsLeadingArticle(string display, string expected)
        => NormalizationProfiles.Dutch.StripLeadingArticle(display).Should().Be(expected);

    [Theory]
    // Без провідного токена-артикля (одне слово, або артикль лише як підрядок) — нема що знімати.
    [InlineData("Hund")]
    [InlineData("der")]
    [InlineData("deugd")]
    [InlineData("derby")]
    public void German_ReturnsNullWhenNoLeadingArticleToken(string display)
        => NormalizationProfiles.German.StripLeadingArticle(display).Should().BeNull();

    [Theory]
    // Мови без артиклевих словникових форм нічого не відкидають (їх Articles порожні).
    [InlineData("en", "the cat")]
    [InlineData("pl", "ten dom")]
    [InlineData("uk", "ця миша")]
    public void LanguagesWithoutArticles_StripNothing(string code, string display)
        => NormalizationProfiles.ByCode(code)!.StripLeadingArticle(display).Should().BeNull();

    [Fact]
    public void Registry_ExposesDeAndNlProfilesWithExpectedArticles()
    {
        NormalizationProfiles.ByCode("de")!.Articles.Should().BeEquivalentTo("der", "die", "das");
        NormalizationProfiles.ByCode("nl")!.Articles.Should().BeEquivalentTo("de", "het");
        NormalizationProfiles.All.Select(profile => profile.Code)
            .Should().BeEquivalentTo("uk", "pl", "en", "de", "nl");
    }

    [Fact]
    public void UnionFold_CarriesGermanExpansionsAcrossAllProfiles()
        => NormalizationProfiles.FoldSubstitutions.Should()
            .Contain(new KeyValuePair<char, string>('ß', "ss"))
            .And.Contain(new KeyValuePair<char, string>('ä', "ae"))
            .And.Contain(new KeyValuePair<char, string>('ł', "l"));
}
