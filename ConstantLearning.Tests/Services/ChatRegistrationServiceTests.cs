using ConstantLearning.Platform.Persistence;
using ConstantLearning.Data.Entities;
using ConstantLearning.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace ConstantLearning.Tests.Services;

public class ChatRegistrationServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly Mock<ILogger<ChatRegistrationService>> _loggerMock;
    private readonly ChatRegistrationService _service;

    public ChatRegistrationServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _loggerMock = new Mock<ILogger<ChatRegistrationService>>();
        _service = new ChatRegistrationService(_context, _loggerMock.Object);
    }

    [Fact]
    public async Task IsChatRegisteredAsync_ReturnsFalse_WhenChatNotRegistered()
    {
        // Дія
        var result = await _service.IsChatRegisteredAsync(12345);

        // Перевірка
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsChatRegisteredAsync_ReturnsTrue_WhenChatIsActive()
    {
        // Підготовка
        _context.ChatRegistrations.Add(new ChatRegistration
        {
            ChatId = 12345,
            IsActive = true
        });
        await _context.SaveChangesAsync();

        // Дія
        var result = await _service.IsChatRegisteredAsync(12345);

        // Перевірка
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsChatRegisteredAsync_ReturnsFalse_WhenChatIsInactive()
    {
        // Підготовка
        _context.ChatRegistrations.Add(new ChatRegistration
        {
            ChatId = 12345,
            IsActive = false
        });
        await _context.SaveChangesAsync();

        // Дія
        var result = await _service.IsChatRegisteredAsync(12345);

        // Перевірка
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RegisterChatAsync_CreatesNewRegistration_WhenChatNotExists()
    {
        // Дія
        var result = await _service.RegisterChatAsync(12345, "Test Chat");

        // Перевірка
        result.Should().NotBeNull();
        result.ChatId.Should().Be(12345);
        result.ChatTitle.Should().Be("Test Chat");
        result.IsActive.Should().BeTrue();
        result.RepetitionTime.Should().Be("09:00");
        result.NewWordsTime.Should().Be("20:00");
        result.NewWordsCount.Should().Be(3);
        result.RepetitionWordsCount.Should().Be(10);

        var dbChat = await _context.ChatRegistrations.FirstOrDefaultAsync(c => c.ChatId == 12345);
        dbChat.Should().NotBeNull();
    }

    [Fact]
    public async Task RegisterChatAsync_ReactivatesChat_WhenChatExistsButInactive()
    {
        // Підготовка
        _context.ChatRegistrations.Add(new ChatRegistration
        {
            ChatId = 12345,
            ChatTitle = "Old Title",
            IsActive = false
        });
        await _context.SaveChangesAsync();

        // Дія
        var result = await _service.RegisterChatAsync(12345, "New Title");

        // Перевірка
        result.Should().NotBeNull();
        result.IsActive.Should().BeTrue();
        result.ChatTitle.Should().Be("New Title");

        var count = await _context.ChatRegistrations.CountAsync(c => c.ChatId == 12345);
        count.Should().Be(1);
    }

    [Fact]
    public async Task RegisterChatAsync_ReturnsExisting_WhenChatAlreadyActive()
    {
        // Підготовка
        var existing = new ChatRegistration
        {
            ChatId = 12345,
            ChatTitle = "Existing Chat",
            IsActive = true
        };
        _context.ChatRegistrations.Add(existing);
        await _context.SaveChangesAsync();

        // Дія
        var result = await _service.RegisterChatAsync(12345, "New Title");

        // Перевірка
        result.ChatId.Should().Be(12345);
        result.ChatTitle.Should().Be("Existing Chat");

        var count = await _context.ChatRegistrations.CountAsync(c => c.ChatId == 12345);
        count.Should().Be(1);
    }

    [Fact]
    public async Task GetAllActiveChatIdsAsync_ReturnsOnlyActiveChats()
    {
        // Підготовка
        _context.ChatRegistrations.AddRange(
            new ChatRegistration { ChatId = 1, IsActive = true },
            new ChatRegistration { ChatId = 2, IsActive = false },
            new ChatRegistration { ChatId = 3, IsActive = true }
        );
        await _context.SaveChangesAsync();

        // Дія
        var result = await _service.GetAllActiveChatIdsAsync();

        // Перевірка
        result.Should().HaveCount(2);
        result.Should().Contain(new[] { 1L, 3L });
    }

    [Fact]
    public async Task DeactivateChatAsync_SetsIsActiveToFalse()
    {
        // Підготовка
        _context.ChatRegistrations.Add(new ChatRegistration
        {
            ChatId = 12345,
            IsActive = true
        });
        await _context.SaveChangesAsync();

        // Дія
        await _service.DeactivateChatAsync(12345);

        // Перевірка
        var chat = await _context.ChatRegistrations.FirstOrDefaultAsync(c => c.ChatId == 12345);
        chat.Should().NotBeNull();
        chat!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateRepetitionTimeAsync_UpdatesTime_WhenChatIsActive()
    {
        // Підготовка
        _context.ChatRegistrations.Add(new ChatRegistration
        {
            ChatId = 12345,
            IsActive = true,
            RepetitionTime = "09:00"
        });
        await _context.SaveChangesAsync();

        // Дія
        await _service.UpdateRepetitionTimeAsync(12345, "10:30");

        // Перевірка
        var chat = await _context.ChatRegistrations.FirstOrDefaultAsync(c => c.ChatId == 12345);
        chat!.RepetitionTime.Should().Be("10:30");
    }

    [Fact]
    public async Task UpdateRepetitionTimeAsync_ThrowsException_WhenChatNotFound()
    {
        // Дія та перевірка
        await FluentActions.Invoking(() => _service.UpdateRepetitionTimeAsync(12345, "10:30"))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not registered*");
    }

    [Fact]
    public async Task UpdateNewWordsTimeAsync_UpdatesTime_WhenChatIsActive()
    {
        // Підготовка
        _context.ChatRegistrations.Add(new ChatRegistration
        {
            ChatId = 12345,
            IsActive = true,
            NewWordsTime = "20:00"
        });
        await _context.SaveChangesAsync();

        // Дія
        await _service.UpdateNewWordsTimeAsync(12345, "21:00");

        // Перевірка
        var chat = await _context.ChatRegistrations.FirstOrDefaultAsync(c => c.ChatId == 12345);
        chat!.NewWordsTime.Should().Be("21:00");
    }

    [Fact]
    public async Task UpdateWordsCountAsync_UpdatesCounts_WhenChatIsActive()
    {
        // Підготовка
        _context.ChatRegistrations.Add(new ChatRegistration
        {
            ChatId = 12345,
            IsActive = true,
            NewWordsCount = 3,
            RepetitionWordsCount = 10
        });
        await _context.SaveChangesAsync();

        // Дія
        await _service.UpdateWordsCountAsync(12345, 5, 8);

        // Перевірка
        var chat = await _context.ChatRegistrations.FirstOrDefaultAsync(c => c.ChatId == 12345);
        chat!.NewWordsCount.Should().Be(5);
        chat.RepetitionWordsCount.Should().Be(8);
    }

    [Fact]
    public async Task UpdateWordsCountAsync_ThrowsException_WhenChatNotFound()
    {
        // Дія та перевірка
        await FluentActions.Invoking(() => _service.UpdateWordsCountAsync(12345, 5, 8))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not registered*");
    }

    [Fact]
    public async Task GetChatRegistrationAsync_ReturnsChat_WhenActive()
    {
        // Підготовка
        _context.ChatRegistrations.Add(new ChatRegistration
        {
            ChatId = 12345,
            IsActive = true,
            ChatTitle = "Test Chat"
        });
        await _context.SaveChangesAsync();

        // Дія
        var result = await _service.GetChatRegistrationAsync(12345);

        // Перевірка
        result.Should().NotBeNull();
        result!.ChatId.Should().Be(12345);
        result.ChatTitle.Should().Be("Test Chat");
    }

    [Fact]
    public async Task GetChatRegistrationAsync_ReturnsNull_WhenChatInactive()
    {
        // Підготовка
        _context.ChatRegistrations.Add(new ChatRegistration
        {
            ChatId = 12345,
            IsActive = false
        });
        await _context.SaveChangesAsync();

        // Дія
        var result = await _service.GetChatRegistrationAsync(12345);

        // Перевірка
        result.Should().BeNull();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
