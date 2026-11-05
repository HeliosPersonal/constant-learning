using ConstantLearning.Platform.Persistence;
using ConstantLearning.Data.Entities;
using ConstantLearning.Services;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace ConstantLearning.Tests.Services;

public class ProgressServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly Mock<ILogger<ProgressService>> _loggerMock;
    private readonly ProgressService _service;

    public ProgressServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _loggerMock = new Mock<ILogger<ProgressService>>();
        _service = new ProgressService(_context, _loggerMock.Object);
    }

    [Fact]
    public async Task RestartProgressAsync_RemovesAllLearnedWords_ForSpecificChat()
    {
        // Підготовка
        _context.LearnedWords.AddRange(
            new LearnedWord { ChatId = 12345, WordId = 1, LearnedAt = DateTime.UtcNow },
            new LearnedWord { ChatId = 12345, WordId = 2, LearnedAt = DateTime.UtcNow },
            new LearnedWord { ChatId = 67890, WordId = 3, LearnedAt = DateTime.UtcNow }
        );
        await _context.SaveChangesAsync();

        // Дія
        var removedCount = await _service.RestartProgressAsync(12345);

        // Перевірка
        removedCount.Should().Be(2);

        var remainingWords = await _context.LearnedWords.ToListAsync();
        remainingWords.Should().HaveCount(1);
        remainingWords[0].ChatId.Should().Be(67890);
    }

    [Fact]
    public async Task RestartProgressAsync_ReturnsZero_WhenNoLearnedWords()
    {
        // Дія
        var removedCount = await _service.RestartProgressAsync(12345);

        // Перевірка
        removedCount.Should().Be(0);
    }

    [Fact]
    public async Task RestartProgressAsync_DoesNotAffectOtherChats()
    {
        // Підготовка
        _context.LearnedWords.AddRange(
            new LearnedWord { ChatId = 111, WordId = 1, LearnedAt = DateTime.UtcNow },
            new LearnedWord { ChatId = 222, WordId = 2, LearnedAt = DateTime.UtcNow },
            new LearnedWord { ChatId = 333, WordId = 3, LearnedAt = DateTime.UtcNow }
        );
        await _context.SaveChangesAsync();

        // Дія
        await _service.RestartProgressAsync(222);

        // Перевірка
        var remaining = await _context.LearnedWords.Select(w => w.ChatId).ToListAsync();
        remaining.Should().HaveCount(2);
        remaining.Should().Contain(new[] { 111L, 333L });
        remaining.Should().NotContain(222);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
