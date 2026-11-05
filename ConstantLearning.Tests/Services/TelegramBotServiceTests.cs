using ConstantLearning.Configuration;
using ConstantLearning.Services;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ConstantLearning.Platform.Telegram;

namespace ConstantLearning.Tests.Services;

public class TelegramBotServiceTests
{
    [Fact]
    public void Constructor_InitializesService_Successfully()
    {
        // Підготовка
        var botClientMock = new Mock<ITelegramClient>();
        var botMessagesMock = new Mock<IBotMessagesService>();
        var loggerMock = new Mock<ILogger<TelegramBotService>>();
        var serviceProviderMock = new Mock<IServiceProvider>();
        var learningOptionsMock = new Mock<IOptions<LearningOptions>>();
        learningOptionsMock.Setup(x => x.Value).Returns(new LearningOptions { LearnedWordsChunkSize = 10 });

        // Дія
        var service = new TelegramBotService(
            botClientMock.Object,
            botMessagesMock.Object,
            loggerMock.Object,
            serviceProviderMock.Object,
            learningOptionsMock.Object);

        // Перевірка
        service.Should().NotBeNull();
    }

    [Fact]
    public async Task SendMessageAsync_DoesNotThrow_WithValidParameters()
    {
        // Підготовка
        var botClientMock = new Mock<ITelegramClient>();
        var botMessagesMock = new Mock<IBotMessagesService>();
        var loggerMock = new Mock<ILogger<TelegramBotService>>();
        var serviceProviderMock = new Mock<IServiceProvider>();
        var learningOptionsMock = new Mock<IOptions<LearningOptions>>();
        learningOptionsMock.Setup(x => x.Value).Returns(new LearningOptions { LearnedWordsChunkSize = 10 });

        var service = new TelegramBotService(
            botClientMock.Object,
            botMessagesMock.Object,
            loggerMock.Object,
            serviceProviderMock.Object,
            learningOptionsMock.Object);

        const long chatId = 12345;
        const string text = "Test message";

        // Дія
        Func<Task> act = async () => await service.SendMessageAsync(text, chatId);

        // Перевірка
        await act.Should().NotThrowAsync();
    }
}
