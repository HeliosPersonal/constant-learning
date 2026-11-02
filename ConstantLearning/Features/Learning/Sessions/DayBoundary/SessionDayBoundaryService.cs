using ConstantLearning.Features.Learning.Data;
using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace ConstantLearning.Features.Learning.Sessions.DayBoundary;

/// <summary>Читає наявну активну session та ліниво закриває її після збереженого EndsAt.</summary>
public sealed class SessionDayBoundaryService(AppDbContext db)
{
    /// <summary>
    /// Потребує транзакції й блокування рядка learner викликувачем. Не створює session,
    /// не змінює картки чи облік показів; звільняє унікальне місце активної session до нового INSERT.
    /// </summary>
    public async Task<LearningSession?> GetActiveAsync(Guid learnerId, Instant now,
        CancellationToken cancellationToken = default)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Читання активної session потребує транзакції заблокованого learner.");
        }

        var session = await db.LearningSessions.SingleOrDefaultAsync(value => value.LearnerId == learnerId
            && value.Status == "active", cancellationToken);
        if (session is null || SessionExpiryGuard.CheckAndClose(session, now) is null) return session;

        await db.SaveChangesAsync(cancellationToken);
        return null;
    }
}
