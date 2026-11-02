namespace ConstantLearning.Features.Identity.Aliases;

/// <summary>Стабільний випадковий псевдонім автора без імені й Telegram id.</summary>
public interface IReporterAliasService
{
    /// <summary>Читає лише наявні псевдоніми; не відкриває персональних даних і не змінює профілі.</summary>
    Task<IReadOnlyDictionary<Guid, string>> ReadAsync(IReadOnlyCollection<Guid> learnerIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Потребує транзакції викликувача. Блокує learner і задає alias лише один раз.
    /// Викликати до незбережених змін самого learner; інші tracked сутності не зберігаються.
    /// </summary>
    Task<string> GetOrCreateAsync(Guid learnerId, CancellationToken cancellationToken = default);
}

public interface IReporterAliasGenerator
{
    string Create();
}
