using ConstantLearning.Platform.Metrics;

namespace ConstantLearning.Features.Identity.ProgressReset;

/// <summary>
/// Пише підсумки скидань у лічильник <c>trainer_progress_reset_total</c> і тривалість у гістограму
/// <c>trainer_progress_reset_duration_ms</c>.
/// </summary>
internal sealed class TrainerProgressResetMetrics(TrainerMetrics metrics) : IProgressResetMetrics
{
    public void RecordReset(ProgressResetOutcomeKind outcome, TimeSpan elapsed)
    {
        metrics.RecordProgressReset(outcome switch
        {
            ProgressResetOutcomeKind.Erased => "erased",
            ProgressResetOutcomeKind.Empty => "empty",
            ProgressResetOutcomeKind.IntegrityFailed => "integrity_failed",
            ProgressResetOutcomeKind.Failed => "failed",
            _ => "failed"
        });
        metrics.RecordProgressResetDuration(elapsed);
    }
}
