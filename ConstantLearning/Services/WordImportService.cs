using ConstantLearning.Configuration;
using ConstantLearning.Platform.Persistence;
using ConstantLearning.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace ConstantLearning.Services;

public interface IWordImportService
{
    Task ImportFromCsvAsync();
}

public class WordImportService(
    AppDbContext context,
    ILogger<WordImportService> logger,
    IOptions<WordsImportOptions> importOptions)
    : IWordImportService
{
    private readonly WordsImportOptions _importOptions = importOptions.Value;

    public async Task ImportFromCsvAsync()
    {
        var folderPath = _importOptions.WordsFolder;
        
        if (!Directory.Exists(folderPath))
        {
            logger.LogWarning("Words folder not found: {FolderPath}", folderPath);
            return;
        }

        var existingCount = await context.Words.CountAsync();
        if (existingCount > 0)
        {
            logger.LogInformation("Words already imported ({Count} words). Skipping import.", existingCount);
            return;
        }

        var csvFiles = Directory.GetFiles(folderPath, "*.csv");
        
        if (csvFiles.Length == 0)
        {
            logger.LogWarning("No CSV files found in {FolderPath}", folderPath);
            return;
        }

        logger.LogInformation("Found {Count} CSV file(s) in {FolderPath}", csvFiles.Length, folderPath);

        var allWords = new List<Word>();

        foreach (var filePath in csvFiles)
        {
            var fileName = Path.GetFileName(filePath);
            logger.LogInformation("Processing {FileName}", fileName);

            try
            {
                var words = await ParseCsvFileAsync(filePath, fileName);
                allWords.AddRange(words);
                logger.LogInformation("Loaded {Count} words from {FileName}", words.Count, fileName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing {FileName}", fileName);
            }
        }

        if (allWords.Count > 0)
        {
            await context.Words.AddRangeAsync(allWords);
            await context.SaveChangesAsync();
            logger.LogInformation("Successfully imported {Count} words from {FileCount} file(s)", 
                allWords.Count, csvFiles.Length);
        }
        else
        {
            logger.LogWarning("No words imported from any CSV file");
        }
    }

    private async Task<List<Word>> ParseCsvFileAsync(string filePath, string fileName)
    {
        var lines = await File.ReadAllLinesAsync(filePath);
        var words = new List<Word>();

        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parts = line.Split(',');
            if (parts.Length < 4)
            {
                logger.LogWarning("{FileName}: Invalid CSV line at row {Row}: {Line}", fileName, i + 1, line);
                continue;
            }

            words.Add(new Word
            {
                FrequencyRank = int.Parse(parts[0].Trim(), CultureInfo.InvariantCulture),
                TargetWord = parts[1].Trim(),
                SourceMeaning = parts[2].Trim(),
                PhoneticTranscription = parts[3].Trim(),
                ImportedAt = DateTime.UtcNow
            });
        }

        return words;
    }
}
