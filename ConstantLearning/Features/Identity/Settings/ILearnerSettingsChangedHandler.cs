using ConstantLearning.Features.Learning.Domain.LearnerDay;
using NodaTime;

namespace ConstantLearning.Features.Identity.Settings;

public sealed record LearnerSettingsChanged(Guid LearnerId, Instant ChangedAt, LearnerDayPeriod Day);

/// <summary>Транзакційна точка розширення для перерахунку persistent dispatch rows у T30.</summary>
public interface ILearnerSettingsChangedHandler
{
    /// <summary>
    /// Викликається після збереження налаштувань, до commit у спільному scoped AppDbContext.
    /// Обробник читає актуальні налаштування з БД і змінює лише локальні дані;
    /// зовнішні виклики виконує dispatcher після commit. Помилка скасовує всю операцію.
    /// </summary>
    Task HandleAsync(LearnerSettingsChanged change, CancellationToken cancellationToken);
}
