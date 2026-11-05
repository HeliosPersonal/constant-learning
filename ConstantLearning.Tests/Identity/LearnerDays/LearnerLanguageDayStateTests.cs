using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Features.Identity.LearnerDays;
using ConstantLearning.Platform.Persistence;
using ConstantLearning.Tests.Identity.Settings;
using ConstantLearning.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;

namespace ConstantLearning.Tests.Identity.LearnerDays;

/// <summary>
/// T04 DoD: стан доби ключований на <c>(learner_id, target_language)</c> (ADR-0002). Перевіряємо
/// обчислення доби для НЕ-pl мови, ізоляцію просування доби між мовами (AC-05), лінивий «перший
/// день» порожнього рядка (AC-07) і блокування саме на рядок мови (різні мови не серіалізуються).
/// </summary>
public sealed class LearnerLanguageDayStateTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;

    [DockerFact]
    public async Task LazyRow_ForNonActiveLanguage_YieldsCorrectFirstDay_FromEmptyState()
    {
        var learner = await SeedAsync();
        await using var app = await SettingsTestApplication.StartAsync(_postgres.ConnectionString, Clock);
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var days = scope.ServiceProvider.GetRequiredService<ILearnerDayStateService>();

        await using var transaction = await db.Database.BeginTransactionAsync();
        var state = await days.GetForUpdateAsync(learner.Id, "en");
        Assert.NotNull(state);
        // Порожній рядок 'en' дає коректний «перший день» для довільної мови — межі рахуються так само, як для pl.
        Assert.Equal("en", state.State.TargetLanguage);
        Assert.Equal(new LocalDate(2026, 1, 15), state.Day.Date);
        Assert.Equal("Europe/Warsaw", state.Day.TimeZoneAtStart);
        Assert.Equal(Instant.FromUtc(2026, 1, 15, 23, 0), state.Day.EndsAt);
        await transaction.CommitAsync();

        // Рядок 'en' матеріалізувався; рядка 'pl' іще немає — нова мова не чіпає стан решти мов.
        var languages = await Db.LearnerLanguageStates.AsNoTracking()
            .Select(value => value.TargetLanguage).ToListAsync();
        Assert.Equal(["en"], languages);
    }

    [DockerFact]
    public async Task AdvancingDayOfOneLanguage_LeavesTheOtherLanguageRowUntouched()
    {
        var learner = await SeedAsync();
        await using var app = await SettingsTestApplication.StartAsync(_postgres.ConnectionString, Clock);

        // Матеріалізуємо обидві мови на одній добі 2026-01-15 (окремі закомічені транзакції).
        await ResolveAndCommitAsync(app, learner.Id, "pl");
        await ResolveAndCommitAsync(app, learner.Id, "en");
        Assert.Equal(new LocalDate(2026, 1, 15), await DayDateAsync("pl"));
        Assert.Equal(new LocalDate(2026, 1, 15), await DayDateAsync("en"));

        // П'ять неактивних днів — потім просуваємо добу ЛИШЕ для 'en'.
        Clock.Advance(Duration.FromDays(5));
        await ResolveAndCommitAsync(app, learner.Id, "en");

        // 'en' перейшла на 2026-01-20; 'pl' лишилась на 2026-01-15 — просування доби мови A не торкає рядок мови B (AC-05).
        Assert.Equal(new LocalDate(2026, 1, 20), await DayDateAsync("en"));
        Assert.Equal(new LocalDate(2026, 1, 15), await DayDateAsync("pl"));
    }

    [DockerFact]
    public async Task RowLock_IsPerLanguage_SoDifferentLanguagesDoNotSerialize_ButSameLanguageBlocks()
    {
        var learner = await SeedAsync();
        await using var app = await SettingsTestApplication.StartAsync(_postgres.ConnectionString, Clock);

        // Рядки обох мов мають існувати, щоб FOR UPDATE був чистим тестом блокування (а не вставкою).
        await ResolveAndCommitAsync(app, learner.Id, "pl");
        await ResolveAndCommitAsync(app, learner.Id, "en");

        await using var holderScope = app.Services.CreateAsyncScope();
        var holderDb = holderScope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var holderTransaction = await holderDb.Database.BeginTransactionAsync();
        // Утримуємо блокування рядка 'en' до кінця тесту.
        Assert.NotNull(await holderScope.ServiceProvider.GetRequiredService<ILearnerDayStateService>()
            .GetForUpdateAsync(learner.Id, "en"));

        // Інша мова ('pl') — інший рядок: НЕ блокується, завершується одразу.
        await using var otherScope = app.Services.CreateAsyncScope();
        var otherDb = otherScope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var otherTransaction = await otherDb.Database.BeginTransactionAsync();
        var otherLanguage = otherScope.ServiceProvider.GetRequiredService<ILearnerDayStateService>()
            .GetForUpdateAsync(learner.Id, "pl");
        Assert.Same(otherLanguage, await Task.WhenAny(otherLanguage, Task.Delay(500)));
        Assert.NotNull(await otherLanguage);
        await otherTransaction.CommitAsync();

        // Та сама мова ('en') — той самий рядок: блокується, доки тримач не закомітить.
        await using var waiterScope = app.Services.CreateAsyncScope();
        var waiterDb = waiterScope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var waiterTransaction = await waiterDb.Database.BeginTransactionAsync();
        var sameLanguage = waiterScope.ServiceProvider.GetRequiredService<ILearnerDayStateService>()
            .GetForUpdateAsync(learner.Id, "en");
        Assert.NotSame(sameLanguage, await Task.WhenAny(sameLanguage, Task.Delay(150)));
        await holderTransaction.CommitAsync();
        Assert.NotNull(await sameLanguage.WaitAsync(TimeSpan.FromSeconds(10)));
        await waiterTransaction.CommitAsync();
    }

    private async Task ResolveAndCommitAsync(SettingsTestApplication app, Guid learnerId, string language)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var state = await scope.ServiceProvider.GetRequiredService<ILearnerDayStateService>()
            .GetForUpdateAsync(learnerId, language);
        Assert.NotNull(state);
        await transaction.CommitAsync();
    }

    private async Task<LocalDate?> DayDateAsync(string language) =>
        (await Db.LearnerLanguageStates.AsNoTracking()
            .SingleAsync(value => value.TargetLanguage == language)).CurrentDayDate;

    private async Task<Learner> SeedAsync()
    {
        Clock.Reset(Instant.FromUtc(2026, 1, 15, 11, 0));
        var learner = new Learner
        {
            TelegramUserId = 900000001, TimeZone = "Europe/Warsaw", TimeZoneConfirmed = true,
            DailyNewWordsLimit = 5, OnboardedAt = Clock.GetCurrentInstant(), CreatedAt = Clock.GetCurrentInstant()
        };
        Db.Learners.Add(learner);
        await Db.SaveChangesAsync();
        return learner;
    }
}
