using ConstantLearning.Features.Learning.Domain.LearnerDay;
using NodaTime;

namespace ConstantLearning.Features.Identity.Settings;

/// <param name="TargetLanguage">Активна мова, для якої змінено налаштування доби й нагадування (ADR-0005).</param>
/// <param name="TimeZoneChanged">Пояс learner-а змінився: нагадування всіх мов перераховуються (пояс глобальний).</param>
public sealed record LearnerSettingsChanged(Guid LearnerId, Instant ChangedAt, LearnerDayPeriod Day,
    string TargetLanguage, bool TimeZoneChanged);

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
