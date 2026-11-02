using ConstantLearning.Configuration;
using ConstantLearning.Enums;
using ConstantLearning.Services;
using Microsoft.Extensions.Options;
using Quartz;
using Telegram.Bot.Types.Enums;

namespace ConstantLearning.Jobs;

[DisallowConcurrentExecution]
public class NewWordsJob(
    IWordService wordService,
    ITelegramBotService telegramBotService,
    IMessageFormatterService messageFormatterService,
    IBotMessagesService botMessages,
    ILogger<NewWordsJob> logger,
    IOptions<LearningOptions> learningOptions,
    IServiceProvider serviceProvider)
    : IJob
{
    private readonly LearningOptions _learningOptions = learningOptions.Value;

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Starting new words job check for all registered chats");

            using var scope = serviceProvider.CreateScope();
            var chatRegistrationService = scope.ServiceProvider.GetRequiredService<IChatRegistrationService>();
            var activeChatIds = await chatRegistrationService.GetAllActiveChatIdsAsync();

            if (activeChatIds.Count == 0)
            {
                logger.LogInformation("No registered chats found. Skipping new words job.");
                return;
            }

            var currentTime = DateTime.Now.ToString("HH:mm");
            var currentHour = DateTime.Now.Hour;
            var currentMinute = DateTime.Now.Minute;

            logger.LogInformation("Current time: {CurrentTime}. Checking {Count} chat(s) for scheduled new words", 
                currentTime, activeChatIds.Count);

            var processedCount = 0;

            foreach (var chatId in activeChatIds)
            {
                try
                {
                    var chatRegistration = await chatRegistrationService.GetChatRegistrationAsync(chatId);
                    if (chatRegistration == null)
                        continue;

                    var configuredTime = chatRegistration.NewWordsTime;
                    var timeParts = configuredTime.Split(':');
                    if (timeParts.Length != 2)
                        continue;

                    var configuredHour = int.Parse(timeParts[0]);
                    var configuredMinute = int.Parse(timeParts[1]);

                    if (currentHour == configuredHour && currentMinute == configuredMinute)
                    {
                        await ProcessNewWordsForChatAsync(chatId);
                        processedCount++;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error processing new words check for chat {ChatId}", chatId);
                }
            }

            logger.LogInformation("New words job check completed. Processed {ProcessedCount} chat(s)", processedCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing new words job");
            throw;
        }
    }

    private async Task ProcessNewWordsForChatAsync(long chatId)
    {
        logger.LogInformation("Starting new words for chat {ChatId}", chatId);

        using var scope = serviceProvider.CreateScope();
        var chatRegistrationService = scope.ServiceProvider.GetRequiredService<IChatRegistrationService>();
        var chatRegistration = await chatRegistrationService.GetChatRegistrationAsync(chatId);
        
        if (chatRegistration == null)
        {
            logger.LogWarning("Chat registration not found for chat {ChatId}", chatId);
            return;
        }

        var wordsCount = chatRegistration.NewWordsCount > 0 ? chatRegistration.NewWordsCount : _learningOptions.NewWordsCount;
        var words = await wordService.GetNewWordsAsync(chatId, wordsCount);

        if (words.Count == 0)
        {
            logger.LogInformation("No new words available for chat {ChatId}", chatId);
            var completionMessage = botMessages.GetMessage(BotMessageKey.AllWordsLearned);
            await telegramBotService.SendMessageAsync(completionMessage, chatId);
            return;
        }

        var message = messageFormatterService.FormatNewWords(words);
        await telegramBotService.SendMessageAsync(message, chatId, ParseMode.Html);

        await wordService.MarkWordsAsLearnedAsync(chatId, words.Select(w => w.Id));

        logger.LogInformation("New words completed for chat {ChatId}. Words learned: {Count}", chatId, words.Count);
    }
}