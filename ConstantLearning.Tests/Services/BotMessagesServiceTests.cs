using ConstantLearning.Configuration;
using ConstantLearning.Enums;
using ConstantLearning.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace ConstantLearning.Tests.Services;

public class BotMessagesServiceTests
{
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<IOptions<LanguageOptions>> _languageOptionsMock;
    private readonly Mock<ILogger<BotMessagesService>> _loggerMock;
    private readonly BotMessagesService _service;

    public BotMessagesServiceTests()
    {
        _configurationMock = new Mock<IConfiguration>();
        _languageOptionsMock = new Mock<IOptions<LanguageOptions>>();
        _languageOptionsMock.Setup(x => x.Value).Returns(new LanguageOptions
        {
            SourceLanguageCode = "uk",
            TargetLanguageCode = "pl",
            SourceLanguage = "Ukrainian",
            TargetLanguage = "Polish"
        });

        _loggerMock = new Mock<ILogger<BotMessagesService>>();
        
        // Налаштувати імітацію конфігурації для тестових повідомлень
        var ukSection = new Mock<IConfigurationSection>();
        var messagesCollection = new List<IConfigurationSection>
        {
            CreateConfigSection("ChatAlreadyRegistered", "вже зареєстровано"),
            CreateConfigSection("ProgressRestarted", "Прогрес скинуто!\n\nВидалено {0} вивчених слів."),
            CreateConfigSection("WordsCountSet", "✅ Кількість слів оновлено!\n\n🆕 Нові слова: {0}\n🔄 Повторення: {1}"),
            CreateConfigSection("LearningStopped", "Навчання зупинено")
        };
        
        ukSection.Setup(x => x.GetChildren()).Returns(messagesCollection);
        _configurationMock.Setup(x => x.GetSection("uk")).Returns(ukSection.Object);
        
        _service = new BotMessagesService(_configurationMock.Object, _languageOptionsMock.Object, _loggerMock.Object);
    }
    
    private static IConfigurationSection CreateConfigSection(string key, string value)
    {
        var section = new Mock<IConfigurationSection>();
        section.Setup(x => x.Key).Returns(key);
        section.Setup(x => x.Value).Returns(value);
        return section.Object;
    }

    [Fact]
    public void GetMessage_ReturnsCorrectMessage_ForSimpleKey()
    {
        // Дія
        var result = _service.GetMessage(BotMessageKey.ChatAlreadyRegistered);

        // Перевірка
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("вже зареєстровано");
    }

    [Fact]
    public void GetMessage_FormatsMessage_WithSingleArgument()
    {
        // Дія
        var result = _service.GetMessage(BotMessageKey.ProgressRestarted, 5);

        // Перевірка
        result.Should().Contain("5");
    }

    [Fact]
    public void GetMessage_FormatsMessage_WithMultipleArguments()
    {
        // Дія
        var result = _service.GetMessage(BotMessageKey.WordsCountSet, 3, 10);

        // Перевірка
        result.Should().Contain("3");
        result.Should().Contain("10");
    }


    [Fact]
    public void GetMessage_ReturnsUkrainianMessage_WhenLanguageCodeIsUk()
    {
        // Дія
        var result = _service.GetMessage(BotMessageKey.LearningStopped);

        // Перевірка
        result.Should().Contain("зупинено");
    }
}
