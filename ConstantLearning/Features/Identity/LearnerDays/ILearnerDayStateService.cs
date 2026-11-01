using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Features.Learning.Domain.LearnerDay;

namespace ConstantLearning.Features.Identity.LearnerDays;

public sealed record LearnerDayState(Learner Learner, LearnerDayPeriod Day);

/// <summary>Сталий стан доби й дозрілого ліміту для Identity, Learning і Notifications.</summary>
public interface ILearnerDayStateService
{
    /// <summary>
    /// Потребує відкритої транзакції спільного scoped AppDbContext. Блокує рядок learner,
    /// ліниво ініціалізує або актуалізує добу й зберігає її в цій транзакції.
    /// Викликайте до змін агрегату; блокування діє до commit або rollback викликувача.
    /// Відсутній або незавершений learner повертається як null.
    /// </summary>
    Task<LearnerDayState?> GetForUpdateAsync(Guid learnerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Змінює пояс заблокованого агрегату й межу pending limit; викликувач зберігає зміни
    /// разом зі своєю операцією. Наявні session та NewWordsAvailableFrom не змінюються.
    /// </summary>
    LearnerDayState ChangeTimeZone(LearnerDayState current, string timeZone);
}
