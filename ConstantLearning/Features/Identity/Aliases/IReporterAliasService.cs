namespace ConstantLearning.Features.Identity.Aliases;

/// <summary>Стабільний випадковий псевдонім автора без імені й Telegram id.</summary>
public interface IReporterAliasService
{
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
