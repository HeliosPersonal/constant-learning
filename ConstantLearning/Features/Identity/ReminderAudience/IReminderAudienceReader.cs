using NodaTime;

namespace ConstantLearning.Features.Identity.ReminderAudience;

/// <summary>Налаштування нагадування однієї мови learner-а (ADR-0005); ліміт і «нові слова» — зі стану цієї мови.</summary>
public sealed record ReminderPreferences(string TargetLanguage, LocalTime LocalTime, short DaysOfWeek,
    int DailyNewWordsLimit, Instant? NewWordsAvailableFrom);

/// <summary>Налаштування нагадувань без блокування learner у транзакції dispatcher.</summary>
public interface IReminderAudienceReader
{
    /// <summary>Сторінка learner-ів (за зростанням id), у яких є хоча б одне нагадування.</summary>
    Task<IReadOnlyList<Guid>> ReadRecipientsAsync(Guid? after, int take, CancellationToken cancellationToken = default);

    /// <summary>Нагадування learner-а по всіх мовах; порожній список, якщо нагадувань немає або пояс не підтверджено.</summary>
    Task<IReadOnlyList<ReminderPreferences>> ReadAsync(Guid learnerId, CancellationToken cancellationToken = default);
}
