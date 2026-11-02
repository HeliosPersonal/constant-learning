using ConstantLearning.Platform.Persistence;
using ConstantLearning.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ConstantLearning.Services;

public interface IWordService
{
    Task<List<Word>> GetRandomLearnedWordsAsync(long chatId, int count);
    Task<List<Word>> GetNewWordsAsync(long chatId, int count);
    Task<List<Word>> GetAllLearnedWordsAsync(long chatId);
    Task MarkWordsAsLearnedAsync(long chatId, IEnumerable<int> wordIds);
    Task UpdateRepetitionAsync(long chatId, IEnumerable<int> wordIds);
}

public class WordService(AppDbContext context) : IWordService
{
    public async Task<List<Word>> GetRandomLearnedWordsAsync(long chatId, int count)
    {
        var learnedWordIds = await context.LearnedWords
            .Where(lw => lw.ChatId == chatId)
            .Select(lw => lw.WordId)
            .ToListAsync();

        if (learnedWordIds.Count == 0)
        {
            return [];
        }

        var selectedIds = learnedWordIds
            .OrderBy(_ => Random.Shared.Next())
            .Take(Math.Min(count, learnedWordIds.Count))
            .ToList();

        return await context.Words
            .Where(w => selectedIds.Contains(w.Id))
            .ToListAsync();
    }

    public async Task<List<Word>> GetNewWordsAsync(long chatId, int count)
    {
        var learnedWordIds = await context.LearnedWords
            .Where(lw => lw.ChatId == chatId)
            .Select(lw => lw.WordId)
            .ToListAsync();

        return await context.Words
            .Where(w => !learnedWordIds.Contains(w.Id))
            .OrderBy(w => w.FrequencyRank)
            .Take(count)
            .ToListAsync();
    }

    public async Task<List<Word>> GetAllLearnedWordsAsync(long chatId)
    {
        var learnedWordIds = await context.LearnedWords
            .Where(lw => lw.ChatId == chatId)
            .OrderBy(lw => lw.LearnedAt)
            .Select(lw => lw.WordId)
            .ToListAsync();

        if (learnedWordIds.Count == 0)
        {
            return [];
        }

        var words = await context.Words
            .Where(w => learnedWordIds.Contains(w.Id))
            .ToListAsync();

        return words
            .OrderBy(w => learnedWordIds.IndexOf(w.Id))
            .ToList();
    }

    public async Task MarkWordsAsLearnedAsync(long chatId, IEnumerable<int> wordIds)
    {
        var existingIds = await context.LearnedWords
            .Where(lw => lw.ChatId == chatId && wordIds.Contains(lw.WordId))
            .Select(lw => lw.WordId)
            .ToListAsync();

        var newWordIds = wordIds.Except(existingIds).ToList();

        foreach (var wordId in newWordIds)
        {
            context.LearnedWords.Add(new LearnedWord
            {
                ChatId = chatId,
                WordId = wordId,
                LearnedAt = DateTime.UtcNow,
                LastRepeatedAt = DateTime.UtcNow,
                RepetitionCount = 0
            });
        }

        await context.SaveChangesAsync();
    }

    public async Task UpdateRepetitionAsync(long chatId, IEnumerable<int> wordIds)
    {
        var learnedWords = await context.LearnedWords
            .Where(lw => lw.ChatId == chatId && wordIds.Contains(lw.WordId))
            .ToListAsync();

        foreach (var learnedWord in learnedWords)
        {
            learnedWord.LastRepeatedAt = DateTime.UtcNow;
            learnedWord.RepetitionCount++;
        }

        await context.SaveChangesAsync();
    }
}
