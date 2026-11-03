using ConstantLearning.Features.Identity.ProgressReset;
using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConstantLearning.Features.Content.Reports;

/// <summary>
/// Знімає привʼязку content reports learner до його відповідей до того, як Learning видалить відповіді
/// (<see cref="ProgressResetPhase.DetachReferences"/>). Сам report, його псевдонім і статус лишаються (AC-10).
/// </summary>
internal sealed class ContentProgressResetHandler(AppDbContext db) : IProgressResetHandler
{
    public ProgressResetPhase Phase => ProgressResetPhase.DetachReferences;

    public Task<bool> HasPendingDataAsync(Guid learnerId, CancellationToken cancellationToken) =>
        db.ContentReports.AnyAsync(report => report.LearnerId == learnerId && report.AnswerEventId != null, cancellationToken);

    public Task EraseAsync(ProgressResetRequested request, CancellationToken cancellationToken) =>
        db.ContentReports
            .Where(report => report.LearnerId == request.LearnerId && report.AnswerEventId != null)
            .ExecuteUpdateAsync(set => set.SetProperty(report => report.AnswerEventId, (Guid?)null), cancellationToken);
}
