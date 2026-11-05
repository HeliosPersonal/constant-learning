using ConstantLearning.Features.Content.References;
using AwesomeAssertions;

namespace ConstantLearning.Tests.Content.References;

public sealed class WikislownikParserTests
{
    private readonly WikislownikParser _parser = new(new PolishIpaConverter());

    [Fact]
    public void SupportedFixture_ExtractsOnlyPolishSection()
    {
        var parsed = _parser.Parse(ReferenceFixtures.Read("supported"));
        parsed.ManualDecisions.Should().BeEmpty();
        parsed.Values.Should().BeEquivalentTo(new ReferenceValue[]
        {
            new("ipa", "kɔt"), new("transcription", "кот"), new("translation", "кіт")
        });
    }

    [Fact]
    public void UnsupportedFixture_ProducesManualDecisions()
    {
        var parsed = _parser.Parse(ReferenceFixtures.Read("unsupported"));
        parsed.ManualDecisions.Should().Contain(new ReferenceValue("transcription", "unsupported_ipa_symbol"));
        parsed.ManualDecisions.Should().Contain(new ReferenceValue("translation", "unsupported_translation_markup"));
        parsed.Values.Should().NotContain(value => value.Field == "transcription" || value.Field == "translation");
    }

    [Theory]
    [InlineData("{{IPA|kɔt}}", "{{IPA|kɔt}}{{IPA|BAD}}", "transcription")]
    [InlineData("{{IPA|kɔt}}", "{{IPA|kɔt}}{{IPA|BAD|extra}}", "transcription")]
    [InlineData("[[кіт]]", "[[кіт]], {{зменш.|кошеня}}", "translation")]
    [InlineData(": (1.1) zwierzę", ": (1.1) zwierzę\n: (1.2) inne znaczenie", "translation")]
    [InlineData("ukraiński: (1.1)", "ukraiński: (1.2)", "translation")]
    public void AmbiguousInput_IsNotAutomaticallyUsable(string original, string replacement, string field)
    {
        var parsed = _parser.Parse(ReferenceFixtures.Read("supported").Replace(original, replacement));
        parsed.ManualDecisions.Should().Contain(value => value.Field == field);
    }

    [Fact]
    public void MissingPolishSection_DoesNotThrow()
    {
        _parser.Parse("== kot ({{język czeski}}) ==").ManualDecisions.Should().HaveCount(2);
    }

    [Theory]
    [InlineData("kɔt", "кот")]
    [InlineData("/ˈdɔm/", "до́м")]
    [InlineData("[t͡ʂɛɕt͡ɕ]", "чесьць")]
    [InlineData("ˈɡura", "ґу́ра")]
    public void IpaWhitelist_ConvertsSupportedSamples(string ipa, string expected)
    {
        new PolishIpaConverter().Convert(ipa).Should().Be(new IpaConversion(expected, null));
    }

    [Theory]
    [InlineData("vɔ̃ʂ")]
    [InlineData("kɔt/kɔd")]
    [InlineData("kɔːt")]
    [InlineData("(kɔt)")]
    [InlineData("ˈkɔˈtɔ")]
    [InlineData("ˈkrt")]
    [InlineData("krt")]
    [InlineData("ɡura")]
    [InlineData("")]
    public void UnsupportedIpa_ProducesNoPartialValue(string ipa)
    {
        var result = new PolishIpaConverter().Convert(ipa);
        result.Value.Should().BeNull();
        result.ManualReason.Should().NotBeNullOrWhiteSpace();
    }
}
