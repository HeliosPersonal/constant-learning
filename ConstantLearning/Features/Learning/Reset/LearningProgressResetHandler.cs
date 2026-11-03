using ConstantLearning.Features.Identity.ProgressReset;
using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConstantLearning.Features.Learning.Reset;

/// <summary>
/// Стирає навчальні дані learner (<see cref="ProgressResetPhase.EraseProgress"/>): стан слів, усі session
/// будь-якого виду з картками, відповіді та нейтралізації оскаржень. Виконується після Content, який уже
/// зняв привʼязку reports до відповідей — інакше зовнішній ключ без каскаду заблокував би видалення.
/// </summary>
internal sealed class LearningProgressResetHandler(AppDbContext db) : IProgressResetHandler
{
    public ProgressResetPhase Phase => ProgressResetPhase.EraseProgress;

    public async Task<bool> HasPendingDataAsync(Guid learnerId, CancellationToken cancellationToken) =>
        await db.LearnerWords.AnyAsync(word => word.LearnerId == learnerId, cancellationToken)
        || await db.LearningSessions.AnyAsync(session => session.LearnerId == learnerId, cancellationToken)
        || await db.AnswerEvents.AnyAsync(answer => answer.LearnerId == learnerId, cancellationToken);

    public async Task EraseAsync(ProgressResetRequested request, CancellationToken cancellationToken)
    {
        var learnerId = request.LearnerId;

        // Відповіді першими: каскад прибирає нейтралізації (answer_neutralizations.answer_event_id).
        // content_reports.answer_event_id уже обнулив обробник Content, тож зовнішній ключ без каскаду вільний.
        await db.AnswerEvents.Where(answer => answer.LearnerId == learnerId).ExecuteDeleteAsync(cancellationToken);

        // Розриваємо цикл learning_sessions.result_item_id → session_items (RESTRICT) перед видаленням session.
        await db.LearningSessions.Where(session => session.LearnerId == learnerId)
            .ExecuteUpdateAsync(set => set.SetProperty(session => session.ResultItemId, (Guid?)null), cancellationToken);

        // Session усіх видів; каскад прибирає session_items.
        await db.LearningSessions.Where(session => session.LearnerId == learnerId).ExecuteDeleteAsync(cancellationToken);

        await db.LearnerWords.Where(word => word.LearnerId == learnerId).ExecuteDeleteAsync(cancellationToken);
    }
}
