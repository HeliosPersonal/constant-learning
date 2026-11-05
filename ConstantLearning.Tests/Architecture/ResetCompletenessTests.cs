using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Platform;
using ConstantLearning.Platform.Persistence;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using NodaTime.Testing;

namespace ConstantLearning.Tests.Architecture;

/// <summary>
/// Страхує ризик «новий модуль/таблиця learner без обробника скидання» (SAD §11): кожна EF-сутність,
/// що по зовнішніх ключах веде до <see cref="Learner"/>, має або обробник скидання, або явний запис у
/// списку збережених. Нова learner-таблиця поза обома списками провалює перевірку.
/// </summary>
public sealed class ResetCompletenessTests
{
    /// <summary>Таблиці, які стирає обробник скидання (Learning + похідні каскади).</summary>
    private static readonly HashSet<string> ErasedByHandler = new(StringComparer.Ordinal)
    {
        "learner_words",
        "learning_sessions",
        "session_items",
        "answer_events",
        "answer_neutralizations"
    };

    /// <summary>
    /// Явний whitelist збережених learner-таблиць (див. SAD §8 Completeness). Рядок тут — свідоме рішення
    /// НЕ стирати дані; нова learner-таблиця не потрапляє сюди автоматично.
    /// </summary>
    private static readonly HashSet<string> PreservedByDesign = new(StringComparer.Ordinal)
    {
        "reminder_schedules",       // налаштування нагадувань (Identity/Notifications)
        "notification_dispatches",  // черга повідомлень learner
        "publication_log",          // журнал дій content editor (actor_learner_id)
        "content_reports",          // автор-псевдонім лишається; обробник Content лише відвʼязує answer_event_id
        // Shadow-таблиця contract/debt-фази (T03, ADR-0002): наразі нічого її не читає, авторитетний
        // день-стан — на learners. Рядок (learner_id, target_language) свідомо зберігається, як і сам
        // рядок learner. Коли активаційна задача перемкне застосунок на читання/запис цієї таблиці,
        // вона має додати in-place скидання полів доби на мову (не DELETE) і перевірити цей запис.
        "learner_language_states"
    };

    [Fact]
    public void EveryLearnerOwnedTable_HasResetHandlerOrIsExplicitlyPreserved()
    {
        using var provider = BuildModelProvider();
        using var scope = provider.CreateScope();
        var model = scope.ServiceProvider.GetRequiredService<AppDbContext>().Model;

        var learnerTable = model.FindEntityType(typeof(Learner))?.GetTableName()
                           ?? throw new InvalidOperationException("Сутність Learner відсутня в моделі.");

        var ownedTables = model.GetEntityTypes()
            .Where(entity => entity.GetTableName() is { } table && table != learnerTable && IsLearnerOwned(entity, learnerTable))
            .Select(entity => entity.GetTableName()!)
            .ToHashSet(StringComparer.Ordinal);

        ownedTables.Should().NotBeEmpty("модель має містити learner-власні таблиці");

        var covered = new HashSet<string>(ErasedByHandler, StringComparer.Ordinal);
        covered.UnionWith(PreservedByDesign);

        // Нова learner-таблиця без обробника чи запису в whitelist.
        ownedTables.Except(covered).Should().BeEmpty(
            "кожна learner-власна таблиця потребує обробника скидання або запису в PreservedByDesign");

        // Застарілий запис у списках, що більше не вказує на learner.
        covered.Except(ownedTables).Should().BeEmpty(
            "ErasedByHandler/PreservedByDesign не повинні містити таблиць поза learner-власними");
    }

    /// <summary>
    /// Таблиця learner-власна, якщо має прямий зовнішній ключ на <c>learners</c> або на одну з прогрес-таблиць,
    /// які стирає обробник (так залучаються <c>session_items</c> і таблиця нейтралізацій через свої батьківські
    /// таблиці). Ланцюги через збережені таблиці (напр. content_reports → модерація) навмисно не поширюють володіння.
    /// </summary>
    private static bool IsLearnerOwned(IEntityType entity, string learnerTable) =>
        entity.GetForeignKeys().Any(foreignKey =>
            foreignKey.PrincipalEntityType.GetTableName() is { } principalTable
            && (principalTable == learnerTable || ErasedByHandler.Contains(principalTable)));

    private static ServiceProvider BuildModelProvider()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=reset_model;Username=model;Password=model"
        }).Build();

        return new ServiceCollection()
            .AddSingleton<IClock>(new FakeClock(Instant.FromUtc(2026, 10, 31, 9, 0)))
            .AddPlatform(configuration)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
}
