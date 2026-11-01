using ConstantLearning.Platform.Persistence;
using ConstantLearning.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ConstantLearning.Services;

public interface IChatRegistrationService
{
    Task<bool> IsChatRegisteredAsync(long chatId);
    Task<ChatRegistration> RegisterChatAsync(long chatId, string? chatTitle);
    Task<List<long>> GetAllActiveChatIdsAsync();
    Task DeactivateChatAsync(long chatId);
    Task UpdateRepetitionTimeAsync(long chatId, string time);
    Task UpdateNewWordsTimeAsync(long chatId, string time);
    Task UpdateWordsCountAsync(long chatId, int newWordsCount, int repetitionWordsCount);
    Task<ChatRegistration?> GetChatRegistrationAsync(long chatId);
}

public class ChatRegistrationService(AppDbContext context, ILogger<ChatRegistrationService> logger)
    : IChatRegistrationService
{
    public async Task<bool> IsChatRegisteredAsync(long chatId)
    {
        return await context.ChatRegistrations
            .AnyAsync(cr => cr.ChatId == chatId && cr.IsActive);
    }

    public async Task<ChatRegistration> RegisterChatAsync(long chatId, string? chatTitle)
    {
        var existing = await context.ChatRegistrations
            .FirstOrDefaultAsync(cr => cr.ChatId == chatId);

        if (existing != null)
        {
            if (!existing.IsActive)
            {
                existing.IsActive = true;
                existing.ChatTitle = chatTitle;
                await context.SaveChangesAsync();
                logger.LogInformation("Reactivated chat {ChatId} ({ChatTitle})", chatId, chatTitle);
            }
            return existing;
        }

        var registration = new ChatRegistration
        {
            ChatId = chatId,
            ChatTitle = chatTitle,
            RegisteredAt = DateTime.UtcNow,
            IsActive = true
        };

        context.ChatRegistrations.Add(registration);
        await context.SaveChangesAsync();

        logger.LogInformation("Registered new chat {ChatId} ({ChatTitle})", chatId, chatTitle);
        return registration;
    }

    public async Task<List<long>> GetAllActiveChatIdsAsync()
    {
        return await context.ChatRegistrations
            .Where(cr => cr.IsActive)
            .Select(cr => cr.ChatId)
            .ToListAsync();
    }

    public async Task DeactivateChatAsync(long chatId)
    {
        var registration = await context.ChatRegistrations
            .FirstOrDefaultAsync(cr => cr.ChatId == chatId);

        if (registration != null)
        {
            registration.IsActive = false;
            await context.SaveChangesAsync();
            logger.LogInformation("Deactivated chat {ChatId}", chatId);
        }
    }

    public async Task UpdateRepetitionTimeAsync(long chatId, string time)
    {
        var registration = await context.ChatRegistrations
            .FirstOrDefaultAsync(cr => cr.ChatId == chatId && cr.IsActive);

        if (registration == null)
        {
            throw new InvalidOperationException($"Chat {chatId} is not registered or not active");
        }

        registration.RepetitionTime = time;
        await context.SaveChangesAsync();
        logger.LogInformation("Updated repetition time to {Time} for chat {ChatId}", time, chatId);
    }

    public async Task UpdateNewWordsTimeAsync(long chatId, string time)
    {
        var registration = await context.ChatRegistrations
            .FirstOrDefaultAsync(cr => cr.ChatId == chatId && cr.IsActive);

        if (registration == null)
        {
            throw new InvalidOperationException($"Chat {chatId} is not registered or not active");
        }

        registration.NewWordsTime = time;
        await context.SaveChangesAsync();
        logger.LogInformation("Updated new words time to {Time} for chat {ChatId}", time, chatId);
    }

    public async Task UpdateWordsCountAsync(long chatId, int newWordsCount, int repetitionWordsCount)
    {
        var registration = await context.ChatRegistrations
            .FirstOrDefaultAsync(cr => cr.ChatId == chatId && cr.IsActive);

        if (registration == null)
        {
            throw new InvalidOperationException($"Chat {chatId} is not registered or not active");
        }

        registration.NewWordsCount = newWordsCount;
        registration.RepetitionWordsCount = repetitionWordsCount;
        await context.SaveChangesAsync();
        logger.LogInformation("Updated words count: new={NewCount}, repetition={RepCount} for chat {ChatId}", 
            newWordsCount, repetitionWordsCount, chatId);
    }

    public async Task<ChatRegistration?> GetChatRegistrationAsync(long chatId)
    {
        return await context.ChatRegistrations
            .FirstOrDefaultAsync(cr => cr.ChatId == chatId && cr.IsActive);
    }
}
