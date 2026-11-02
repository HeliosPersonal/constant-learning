using ConstantLearning.Configuration;
using ConstantLearning.Services;
using Microsoft.Extensions.Options;
using Quartz;
using Telegram.Bot.Types.Enums;

namespace ConstantLearning.Jobs;

[DisallowConcurrentExecution]
public class RepetitionJob(
    IWordService wordService,
    ITelegramBotService telegramBotService,
    IMessageFormatterService messageFormatterService,
    ILogger<RepetitionJob> logger,
    IOptions<LearningOptions> learningOptions,
    IServiceProvider serviceProvider)
    : IJob
{
    private readonly LearningOptions _learningOptions = learningOptions.Value;

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Starting repetition job check for all registered chats");

            using var scope = serviceProvider.CreateScope();
            var chatRegistrationService = scope.ServiceProvider.GetRequiredService<IChatRegistrationService>();
            var activeChatIds = await chatRegistrationService.GetAllActiveChatIdsAsync();

            if (activeChatIds.Count == 0)
            {
                logger.LogInformation("No registered chats found. Skipping repetition job.");
                return;
            }

            var currentTime = DateTime.Now.ToString("HH:mm");
            var currentHour = DateTime.Now.Hour;
            var currentMinute = DateTime.Now.Minute;

            logger.LogInformation("Current time: {CurrentTime}. Checking {Count} chat(s) for scheduled repetition", 
                currentTime, activeChatIds.Count);

            var processedCount = 0;

            foreach (var chatId in activeChatIds)
            {
                try
                {
                    var chatRegistration = await chatRegistrationService.GetChatRegistrationAsync(chatId);
                    if (chatRegistration == null)
                        continue;

                    var configuredTime = chatRegistration.RepetitionTime;
                    var timeParts = configuredTime.Split(':');
                    if (timeParts.Length != 2)
                        continue;

                    var configuredHour = int.Parse(timeParts[0]);
                    var configuredMinute = int.Parse(timeParts[1]);

                    if (currentHour == configuredHour && currentMinute == configuredMinute)
                    {
                        await ProcessRepetitionForChatAsync(chatId);
                        processedCount++;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error processing repetition check for chat {ChatId}", chatId);
                }
            }

            logger.LogInformation("Repetition job check completed. Processed {ProcessedCount} chat(s)", processedCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing repetition job");
            throw;
        }
    }

    private async Task ProcessRepetitionForChatAsync(long chatId)
    {
        logger.LogInformation("Starting repetition for chat {ChatId}", chatId);

        using var scope = serviceProvider.CreateScope();
        var chatRegistrationService = scope.ServiceProvider.GetRequiredService<IChatRegistrationService>();
        var chatRegistration = await chatRegistrationService.GetChatRegistrationAsync(chatId);
        
        if (chatRegistration == null)
        {
            logger.LogWarning("Chat registration not found for chat {ChatId}", chatId);
            return;
        }

        var wordsCount = chatRegistration.RepetitionWordsCount > 0 ? chatRegistration.RepetitionWordsCount : _learningOptions.RepetitionWordsCount;
        var words = await wordService.GetRandomLearnedWordsAsync(chatId, wordsCount);

        if (words.Count == 0)
        {
            logger.LogInformation("No learned words available for repetition in chat {ChatId}", chatId);
            return;
        }

        var questionsMessage = messageFormatterService.FormatRepetitionQuestions(words);
        await telegramBotService.SendMessageAsync(questionsMessage, chatId);

        await Task.Delay(TimeSpan.FromSeconds(_learningOptions.AnswerDelaySeconds));

        var answersMessage = messageFormatterService.FormatRepetitionAnswers(words);
        await telegramBotService.SendMessageAsync(answersMessage, chatId, ParseMode.Html);

        await wordService.UpdateRepetitionAsync(chatId, words.Select(w => w.Id));

        logger.LogInformation("Repetition completed for chat {ChatId}. Words repeated: {Count}", chatId, words.Count);
    }
}