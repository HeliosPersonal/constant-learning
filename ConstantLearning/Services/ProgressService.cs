using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConstantLearning.Services;

public interface IProgressService
{
    Task<int> RestartProgressAsync(long chatId);
}

public class ProgressService(AppDbContext context, ILogger<ProgressService> logger) : IProgressService
{
    public async Task<int> RestartProgressAsync(long chatId)
    {
        logger.LogInformation("Restarting learning progress for chat {ChatId}", chatId);

        var learnedWords = await context.LearnedWords
            .Where(lw => lw.ChatId == chatId)
            .ToListAsync();
        
        var count = learnedWords.Count;
        
        context.LearnedWords.RemoveRange(learnedWords);
        await context.SaveChangesAsync();

        logger.LogInformation("Progress restarted for chat {ChatId}. Removed {Count} learned words", chatId, count);

        return count;
    }
}
