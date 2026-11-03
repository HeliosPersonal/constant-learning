using System.Text.RegularExpressions;
using ConstantLearning.Features.Identity.ProgressReset;
using ConstantLearning.Platform.Metrics;

namespace ConstantLearning.Tests.Identity.ProgressReset;

public sealed class ProgressResetMetricsTests
{
    [Fact]
    public void RecordReset_IncrementsCounterPerOutcome_AndRecordsDuration()
    {
        var trainer = new TrainerMetrics();
        var metrics = new TrainerProgressResetMetrics(trainer);

        metrics.RecordReset(ProgressResetOutcomeKind.Erased, TimeSpan.FromMilliseconds(120));
        metrics.RecordReset(ProgressResetOutcomeKind.Erased, TimeSpan.FromMilliseconds(80));
        metrics.RecordReset(ProgressResetOutcomeKind.Empty, TimeSpan.Zero);
        metrics.RecordReset(ProgressResetOutcomeKind.IntegrityFailed, TimeSpan.FromMilliseconds(40));
        metrics.RecordReset(ProgressResetOutcomeKind.Failed, TimeSpan.FromMilliseconds(15));

        var export = trainer.ExportPrometheus();

        Assert.Contains("trainer_progress_reset_total{outcome=\"erased\"} 2", export);
        Assert.Contains("trainer_progress_reset_total{outcome=\"empty\"} 1", export);
        Assert.Contains("trainer_progress_reset_total{outcome=\"integrity_failed\"} 1", export);
        Assert.Contains("trainer_progress_reset_total{outcome=\"failed\"} 1", export);
        Assert.Contains("trainer_progress_reset_duration_ms_count 5", export);
    }

    [Fact]
    public void ResetMetricLabels_CarryNoLearnerOrTelegramIdentifier()
    {
        var trainer = new TrainerMetrics();
        var metrics = new TrainerProgressResetMetrics(trainer);
        metrics.RecordReset(ProgressResetOutcomeKind.Erased, TimeSpan.FromMilliseconds(50));

        var export = trainer.ExportPrometheus();

        // Кожен набір міток рядка скидання має містити лише дозволені ключі: outcome (лічильник) або le (гістограма).
        foreach (Match labelSet in Regex.Matches(export, @"trainer_progress_reset[a-z_]*\{(?<labels>[^}]*)\}"))
        {
            foreach (var label in labelSet.Groups["labels"].Value.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var key = label.Split('=')[0].Trim();
                Assert.Contains(key, new[] { "outcome", "le" });
            }
        }

        // Опис (# HELP) може згадувати "learner" словом; ідентифікаторів не має бути в рядках-зразках.
        var sampleLines = export.Split('\n').Where(line => line.StartsWith("trainer_progress_reset", StringComparison.Ordinal));
        foreach (var line in sampleLines)
        {
            Assert.DoesNotContain("learner", line, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("telegram", line, StringComparison.OrdinalIgnoreCase);
        }
    }
}
