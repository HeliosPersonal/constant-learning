using NodaTime;

namespace ConstantLearning.Features.Identity.ProgressReset;

/// <summary>
/// Порядок обробників скидання в межах однієї транзакції. Content знімає привʼязку reports
/// до відповідей (<see cref="DetachReferences"/>) до того, як Learning видалить самі відповіді
/// (<see cref="EraseProgress"/>); інакше зовнішній ключ без каскаду заблокує видалення.
/// </summary>
public enum ProgressResetPhase
{
    DetachReferences = 1,
    EraseProgress = 2
}

/// <summary>Намір скинути прогрес конкретного learner, заблокованого в транзакції.</summary>
public sealed record ProgressResetRequested(Guid LearnerId, Instant RequestedAt);

/// <summary>
/// Транзакційна точка розширення скидання прогресу за зразком <c>ILearnerSettingsChangedHandler</c>.
/// Кожен модуль, що тримає дані learner, реалізує обробник: Identity викликає їх у порядку
/// <see cref="Phase"/> всередині однієї транзакції під блокуванням рядка learner, до commit.
/// </summary>
public interface IProgressResetHandler
{
    /// <summary>Фаза виконання: визначає порядок серед обробників різних модулів.</summary>
    ProgressResetPhase Phase { get; }

    /// <summary>
    /// Чи лишилися в learner дані, якими володіє цей обробник. Викликається до стирання
    /// (щоб визначити, чи був прогрес) і після (перевірка цілісності: має бути false).
    /// </summary>
    Task<bool> HasPendingDataAsync(Guid learnerId, CancellationToken cancellationToken);

    /// <summary>
    /// Стирає дані learner у спільному scoped AppDbContext без commit. Помилка скасовує всю операцію.
    /// </summary>
    Task EraseAsync(ProgressResetRequested request, CancellationToken cancellationToken);
}
