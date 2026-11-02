using ConstantLearning.Configuration;
using ConstantLearning.Enums;
using Microsoft.Extensions.Options;
using ConstantLearning.Platform.Telegram;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace ConstantLearning.Services;

public interface ITelegramBotService
{
    Task SendMessageAsync(string text, long chatId, ParseMode parseMode = ParseMode.Markdown);
    Task HandleUpdateAsync(Update update);
}

public class TelegramBotService(
    ITelegramClient botClient,
    IBotMessagesService botMessages,
    ILogger<TelegramBotService> logger,
    IServiceProvider serviceProvider,
    IOptions<LearningOptions> learningOptions)
    : ITelegramBotService
{
    public async Task SendMessageAsync(string text, long chatId, ParseMode parseMode = ParseMode.Markdown)
    {
        try
        {
            await botClient.SendMessageAsync(
                chatId: chatId,
                text: text,
                parseMode: parseMode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send message to chat {ChatId}", chatId);
            throw;
        }
    }

    public async Task HandleUpdateAsync(Update update)
    {
        try
        {
            if (update is { Type: UpdateType.Message, Message.Text: not null })
            {
                var messageText = update.Message.Text.Split("@").First().Trim();
                var messageChatId = update.Message.Chat.Id;

                logger.LogInformation("Received message from chat {ChatId}: {MessageText}", messageChatId, messageText);

                if (messageText.Equals("/startlearning", StringComparison.OrdinalIgnoreCase))
                {
                    await HandleStartLearningCommand(messageChatId, update.Message.Chat.Title);
                    return;
                }

                if (messageText.Equals("/stoplearning", StringComparison.OrdinalIgnoreCase))
                {
                    await HandleStopLearningCommand(messageChatId);
                    return;
                }

                if (messageText.Equals("/restartprogress", StringComparison.OrdinalIgnoreCase))
                {
                    await HandleRestartProgressCommand(messageChatId);
                    return;
                }

                if (messageText.StartsWith("/setrepetitiontime", StringComparison.OrdinalIgnoreCase))
                {
                    await HandleSetRepetitionTimeCommand(messageChatId, messageText);
                    return;
                }

                if (messageText.StartsWith("/setnewwordstime", StringComparison.OrdinalIgnoreCase))
                {
                    await HandleSetNewWordsTimeCommand(messageChatId, messageText);
                    return;
                }

                if (messageText.StartsWith("/setwordscount", StringComparison.OrdinalIgnoreCase))
                {
                    await HandleSetWordsCountCommand(messageChatId, messageText);
                    return;
                }

                if (messageText.Equals("/getalllearned", StringComparison.OrdinalIgnoreCase))
                {
                    await HandleGetAllLearnedCommand(messageChatId);
                    return;
                }

                if (messageText.Equals("/help", StringComparison.OrdinalIgnoreCase) ||
                    messageText.Equals("/start", StringComparison.OrdinalIgnoreCase))
                {
                    await HandleHelpCommand(messageChatId);
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling update");
        }

        await Task.CompletedTask;
    }

    private async Task HandleStartLearningCommand(long messageChatId, string? chatTitle)
    {
        try
        {
            logger.LogInformation("Processing /startlearning command for chat {ChatId}", messageChatId);

            using var scope = serviceProvider.CreateScope();
            var chatRegistrationService = scope.ServiceProvider.GetRequiredService<IChatRegistrationService>();

            var isAlreadyRegistered = await chatRegistrationService.IsChatRegisteredAsync(messageChatId);

            if (isAlreadyRegistered)
            {
                var message = botMessages.GetMessage(BotMessageKey.ChatAlreadyRegistered);
                await botClient.SendMessageAsync(chatId: messageChatId, text: message, parseMode: ParseMode.Markdown);
                return;
            }

            await chatRegistrationService.RegisterChatAsync(messageChatId, chatTitle);

            var successMessage = botMessages.GetMessage(BotMessageKey.ChatRegisteredSuccess);
            await botClient.SendMessageAsync(chatId: messageChatId, text: successMessage, parseMode: ParseMode.Markdown);

            logger.LogInformation("Chat {ChatId} ({ChatTitle}) successfully registered", messageChatId, chatTitle);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling startlearning command for chat {ChatId}", messageChatId);

            var errorMessage = botMessages.GetMessage(BotMessageKey.RegistrationError);
            await botClient.SendMessageAsync(chatId: messageChatId, text: errorMessage, parseMode: ParseMode.Markdown);
        }
    }

    private async Task HandleStopLearningCommand(long messageChatId)
    {
        try
        {
            logger.LogInformation("Processing /stoplearning command for chat {ChatId}", messageChatId);

            using var scope = serviceProvider.CreateScope();
            var chatRegistrationService = scope.ServiceProvider.GetRequiredService<IChatRegistrationService>();

            if (!await EnsureChatRegisteredAsync(messageChatId, chatRegistrationService))
                return;

            await chatRegistrationService.DeactivateChatAsync(messageChatId);

            var successMessage = botMessages.GetMessage(BotMessageKey.LearningStopped);
            await botClient.SendMessageAsync(chatId: messageChatId, text: successMessage, parseMode: ParseMode.Markdown);

            logger.LogInformation("Chat {ChatId} deactivated", messageChatId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling stoplearning command for chat {ChatId}", messageChatId);

            var errorMessage = botMessages.GetMessage(BotMessageKey.StopLearningError);
            await botClient.SendMessageAsync(chatId: messageChatId, text: errorMessage, parseMode: ParseMode.Markdown);
        }
    }

    private async Task HandleRestartProgressCommand(long messageChatId)
    {
        try
        {
            logger.LogInformation("Processing /restartprogress command for chat {ChatId}", messageChatId);

            using var scope = serviceProvider.CreateScope();
            var progressService = scope.ServiceProvider.GetRequiredService<IProgressService>();

            var removedCount = await progressService.RestartProgressAsync(messageChatId);

            var message = botMessages.GetMessage(BotMessageKey.ProgressRestarted, removedCount);
            await botClient.SendMessageAsync(chatId: messageChatId, text: message, parseMode: ParseMode.Markdown);

            logger.LogInformation("Progress restart completed for chat {ChatId}. Removed {Count} words", messageChatId,
                removedCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling restartprogress command for chat {ChatId}", messageChatId);

            var errorMessage = botMessages.GetMessage(BotMessageKey.RestartProgressError);
            await botClient.SendMessageAsync(chatId: messageChatId, text: errorMessage, parseMode: ParseMode.Markdown);
        }
    }

    private async Task HandleSetRepetitionTimeCommand(long messageChatId, string messageText)
    {
        try
        {
            var parts = messageText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
            {
                var usageMessage = botMessages.GetMessage(BotMessageKey.InvalidTimeCommandFormat,
                    "/setrepetitiontime HH:MM", "/setrepetitiontime 09:30");
                await botClient.SendMessageAsync(chatId: messageChatId, text: usageMessage, parseMode: ParseMode.Markdown);
                return;
            }

            var timeString = parts[1];
            if (!IsValidTimeFormat(timeString))
            {
                var errorMessage = botMessages.GetMessage(BotMessageKey.InvalidTimeFormat, "09:30");
                await botClient.SendMessageAsync(chatId: messageChatId, text: errorMessage, parseMode: ParseMode.Markdown);
                return;
            }

            using var scope = serviceProvider.CreateScope();
            var chatRegistrationService = scope.ServiceProvider.GetRequiredService<IChatRegistrationService>();

            if (!await EnsureChatRegisteredAsync(messageChatId, chatRegistrationService))
                return;

            await chatRegistrationService.UpdateRepetitionTimeAsync(messageChatId, timeString);

            var successMessage = botMessages.GetMessage(BotMessageKey.RepetitionTimeSet, timeString);
            await botClient.SendMessageAsync(chatId: messageChatId, text: successMessage, parseMode: ParseMode.Markdown);

            logger.LogInformation("Repetition time updated to {Time} for chat {ChatId}", timeString, messageChatId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling setrepetitiontime command for chat {ChatId}. Message: {Message}",
                messageChatId, ex.Message);

            var errorMessage = botMessages.GetMessage(BotMessageKey.UpdateTimeError);
            await botClient.SendMessageAsync(chatId: messageChatId, text: errorMessage, parseMode: ParseMode.Markdown);
        }
    }

    private async Task HandleSetNewWordsTimeCommand(long messageChatId, string messageText)
    {
        try
        {
            var parts = messageText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
            {
                var usageMessage = botMessages.GetMessage(BotMessageKey.InvalidTimeCommandFormat,
                    "/setnewwordstime HH:MM", "/setnewwordstime 20:00");
                await botClient.SendMessageAsync(chatId: messageChatId, text: usageMessage, parseMode: ParseMode.Markdown);
                return;
            }

            var timeString = parts[1];
            if (!IsValidTimeFormat(timeString))
            {
                var errorMessage = botMessages.GetMessage(BotMessageKey.InvalidTimeFormat, "20:00");
                await botClient.SendMessageAsync(chatId: messageChatId, text: errorMessage, parseMode: ParseMode.Markdown);
                return;
            }

            using var scope = serviceProvider.CreateScope();
            var chatRegistrationService = scope.ServiceProvider.GetRequiredService<IChatRegistrationService>();

            if (!await EnsureChatRegisteredAsync(messageChatId, chatRegistrationService))
                return;

            await chatRegistrationService.UpdateNewWordsTimeAsync(messageChatId, timeString);

            var successMessage = botMessages.GetMessage(BotMessageKey.NewWordsTimeSet, timeString);
            await botClient.SendMessageAsync(chatId: messageChatId, text: successMessage, parseMode: ParseMode.Markdown);

            logger.LogInformation("New words time updated to {Time} for chat {ChatId}", timeString, messageChatId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling setnewwordstime command for chat {ChatId}. Message: {Message}",
                messageChatId, ex.Message);

            var errorMessage = botMessages.GetMessage(BotMessageKey.UpdateTimeError);
            await botClient.SendMessageAsync(chatId: messageChatId, text: errorMessage, parseMode: ParseMode.Markdown);
        }
    }

    private async Task HandleSetWordsCountCommand(long messageChatId, string messageText)
    {
        try
        {
            var parts = messageText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || parts[1].Length != 2)
            {
                var usageMessage = botMessages.GetMessage(BotMessageKey.InvalidWordsCountFormat);
                await botClient.SendMessageAsync(chatId: messageChatId, text: usageMessage, parseMode: ParseMode.Markdown);
                return;
            }

            var digits = parts[1];
            if (!int.TryParse(digits[0].ToString(), out var newWordsCount) || 
                !int.TryParse(digits[1].ToString(), out var repetitionWordsCount) ||
                newWordsCount < 1 || newWordsCount > 9 ||
                repetitionWordsCount < 1 || repetitionWordsCount > 9)
            {
                var errorMessage = botMessages.GetMessage(BotMessageKey.InvalidWordsCountFormat);
                await botClient.SendMessageAsync(chatId: messageChatId, text: errorMessage, parseMode: ParseMode.Markdown);
                return;
            }

            using var scope = serviceProvider.CreateScope();
            var chatRegistrationService = scope.ServiceProvider.GetRequiredService<IChatRegistrationService>();

            if (!await EnsureChatRegisteredAsync(messageChatId, chatRegistrationService))
                return;

            await chatRegistrationService.UpdateWordsCountAsync(messageChatId, newWordsCount, repetitionWordsCount);

            var successMessage = botMessages.GetMessage(BotMessageKey.WordsCountSet, newWordsCount, repetitionWordsCount);
            await botClient.SendMessageAsync(chatId: messageChatId, text: successMessage, parseMode: ParseMode.Markdown);

            logger.LogInformation("Words count updated: new={NewCount}, repetition={RepCount} for chat {ChatId}", 
                newWordsCount, repetitionWordsCount, messageChatId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling setwordscount command for chat {ChatId}", messageChatId);

            var errorMessage = botMessages.GetMessage(BotMessageKey.UpdateWordsCountError);
            await botClient.SendMessageAsync(chatId: messageChatId, text: errorMessage, parseMode: ParseMode.Markdown);
        }
    }

    /// <summary>
    /// Перевіряє, чи зареєстровано чат, і за потреби надсилає повідомлення про помилку
    /// </summary>
    /// <returns>true, якщо чат зареєстровано; інакше false</returns>
    private async Task<bool> EnsureChatRegisteredAsync(long chatId, IChatRegistrationService chatRegistrationService)
    {
        var isRegistered = await chatRegistrationService.IsChatRegisteredAsync(chatId);

        if (!isRegistered)
        {
            var message = botMessages.GetMessage(BotMessageKey.ChatNotRegistered);
            await botClient.SendMessageAsync(chatId: chatId, text: message, parseMode: ParseMode.Markdown);
        }

        return isRegistered;
    }

    private static bool IsValidTimeFormat(string timeString)
    {
        if (string.IsNullOrWhiteSpace(timeString))
            return false;

        var parts = timeString.Split(':');
        if (parts.Length != 2)
            return false;

        if (!int.TryParse(parts[0], out var hour) || !int.TryParse(parts[1], out var minute))
            return false;

        return hour >= 0 && hour <= 23 && minute >= 0 && minute <= 59;
    }

    private async Task HandleGetAllLearnedCommand(long messageChatId)
    {
        try
        {
            logger.LogInformation("Processing /getalllearned command for chat {ChatId}", messageChatId);

            using var scope = serviceProvider.CreateScope();
            var chatRegistrationService = scope.ServiceProvider.GetRequiredService<IChatRegistrationService>();
            var wordService = scope.ServiceProvider.GetRequiredService<IWordService>();

            if (!await EnsureChatRegisteredAsync(messageChatId, chatRegistrationService))
                return;

            var learnedWords = await wordService.GetAllLearnedWordsAsync(messageChatId);

            if (learnedWords.Count == 0)
            {
                var noWordsMessage = botMessages.GetMessage(BotMessageKey.NoLearnedWords);
                await botClient.SendMessageAsync(chatId: messageChatId, text: noWordsMessage, parseMode: ParseMode.Markdown);
                return;
            }

            var chunkSize = learningOptions.Value.LearnedWordsChunkSize;
            var totalWords = learnedWords.Count;
            var chunks = learnedWords.Chunk(chunkSize).ToList();

            for (var i = 0; i < chunks.Count; i++)
            {
                var chunk = chunks[i];
                var startIndex = i * chunkSize + 1;
                var endIndex = Math.Min((i + 1) * chunkSize, totalWords);
                
                var header = botMessages.GetMessage(BotMessageKey.LearnedWordsHeader, $"{startIndex}-{endIndex}", totalWords);
                var wordsList = string.Join("\n", chunk.Select((w, idx) => 
                    $"{startIndex + idx}. {w.TargetWord} [{w.PhoneticTranscription}] → {w.SourceMeaning}"));
                
                var message = $"{header}\n\n{wordsList}";
                
                await botClient.SendMessageAsync(chatId: messageChatId, text: message, parseMode: ParseMode.Markdown);
                
                if (i < chunks.Count - 1)
                {
                    await Task.Delay(500);
                }
            }

            logger.LogInformation("Sent {ChunkCount} messages with {TotalWords} learned words to chat {ChatId}", 
                chunks.Count, totalWords, messageChatId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling getalllearned command for chat {ChatId}", messageChatId);

            var errorMessage = botMessages.GetMessage(BotMessageKey.GetAllLearnedError);
            await botClient.SendMessageAsync(chatId: messageChatId, text: errorMessage, parseMode: ParseMode.Markdown);
        }
    }

    private async Task HandleHelpCommand(long messageChatId)
    {
        var message = botMessages.GetMessage(BotMessageKey.Help);
        await botClient.SendMessageAsync(chatId: messageChatId, text: message, parseMode: ParseMode.Markdown);
    }
}
