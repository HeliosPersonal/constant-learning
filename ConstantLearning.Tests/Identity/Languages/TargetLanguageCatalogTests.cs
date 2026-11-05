using ConstantLearning.Features.Identity.Languages;
using ConstantLearning.Tests.Fixtures;
using ConstantLearning.Tests.Infrastructure;
using AwesomeAssertions;

namespace ConstantLearning.Tests.Identity.Languages;

public sealed class TargetLanguageCatalogTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    // Інтеграційні тести конструюють сервіси модуля напряму (як-от DictionaryImportService): оснастка
    // CreateServices реєструє лише AppDbContext/IClock, а не AddIdentity (той тягне AddAuthentication).
    private ITargetLanguageCatalog Catalog => new TargetLanguageCatalog(Db);

    [DockerFact]
    public async Task List_ReturnsSeededLanguagesInSortOrder()
    {
        var languages = await Catalog.ListAsync();

        languages.Select(language => language.Code).Should().Equal("pl", "en", "de", "nl");
        languages.Single(language => language.Code == "pl").IsEnabled.Should().BeTrue();
        languages.Where(language => language.Code != "pl").Should().OnlyContain(language => !language.IsEnabled);
        languages.Should().OnlyContain(language => language.NormalizationProfile == language.Code);
    }

    [DockerFact]
    public async Task Find_ReturnsLanguageInsertedViaFixture_ThenAbsentAfterRemoval()
    {
        var language = TargetLanguageFixtures.NewEnabledLanguage("zz", "en", clock: Clock);
        Db.TargetLanguages.Add(language);
        await Db.SaveChangesAsync();

        try
        {
            var found = await Catalog.FindAsync("zz");
            found.Should().NotBeNull();
            found!.IsEnabled.Should().BeTrue();
            found.NormalizationProfile.Should().Be("en");
        }
        finally
        {
            Db.TargetLanguages.Remove(language);
            await Db.SaveChangesAsync();
        }

        (await Catalog.FindAsync("zz")).Should().BeNull();
    }
}
