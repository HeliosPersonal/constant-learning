using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Features.Learning.Domain.LearnerDay;

namespace ConstantLearning.Features.Identity.LearnerDays;

/// <summary>
/// Стан доби пари (learner, цільова мова): <paramref name="State"/> — авторитетне сховище обліку доби
/// (ADR-0002), <paramref name="Learner"/> лишається носієм поясу й профілю. <paramref name="Day"/> — обчислені межі.
/// </summary>
public sealed record LearnerDayState(Learner Learner, LearnerLanguageState State, LearnerDayPeriod Day);

/// <summary>Сталий стан доби й дозрілого ліміту для Identity, Learning і Notifications.</summary>
public interface ILearnerDayStateService
{
    /// <summary>
    /// Потребує відкритої транзакції спільного scoped AppDbContext. Блокує рядок
    /// <c>(learner_id, target_language)</c> активної мови learner, ліниво ініціалізує або актуалізує
    /// добу й зберігає її в цій транзакції. Викликайте до змін агрегату; блокування діє до commit або
    /// rollback викликувача. Відсутній або незавершений learner повертається як null.
    /// </summary>
    Task<LearnerDayState?> GetForUpdateAsync(Guid learnerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Те саме для явної цільової мови (старт мови T12, тести ізоляції). Рядок стану матеріалізується
    /// ліниво, якщо його ще немає (AC-07). Блокування — лише на рядок цієї мови.
    /// </summary>
    Task<LearnerDayState?> GetForUpdateAsync(Guid learnerId, string targetLanguage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Змінює пояс заблокованого агрегату й межу pending limit; викликувач зберігає зміни
    /// разом зі своєю операцією. Наявні session та NewWordsAvailableFrom не змінюються.
    /// </summary>
    LearnerDayState ChangeTimeZone(LearnerDayState current, string timeZone);
}
