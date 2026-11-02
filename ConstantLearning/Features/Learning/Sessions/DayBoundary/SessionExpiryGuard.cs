using ConstantLearning.Features.Learning.Data;
using ConstantLearning.Features.Learning.Sessions.Start;
using NodaTime;

namespace ConstantLearning.Features.Learning.Sessions.DayBoundary;

/// <summary>Перевіряє незмінну UTC-межу session незалежно від поточного поясу learner.</summary>
public static class SessionExpiryGuard
{
    /// <summary>
    /// Викликати після перевірки власника, до будь-якого зарахування дії над карткою.
    /// При помилці викликувач зберігає й комітить закриття session, потім повертає HTTP 409.
    /// Уже завершена session не втрачає результат; повторна перевірка не змінює історію.
    /// </summary>
    public static SessionError? CheckAndClose(LearningSession session, Instant now)
    {
        if (now < session.EndsAt) return null;

        if (session.Status == "active")
        {
            session.Status = "incomplete";
            session.CompletedAt = null;
        }

        return new SessionError("session.expired", "Заняття минулої доби закрито — почніть нове.");
    }
}
