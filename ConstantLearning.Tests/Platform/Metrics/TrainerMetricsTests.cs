using System.Globalization;
using ConstantLearning.Platform.Metrics;
using AwesomeAssertions;

namespace ConstantLearning.Tests.Platform.Metrics;

public class TrainerMetricsTests
{
    [Fact]
    public void Histograms_ExposeCumulativeBucketsAndInvariantSums()
    {
        var metrics = new TrainerMetrics();
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("uk-UA");
            metrics.RecordTodayReady(840);
            metrics.RecordAnswer(TimeSpan.FromMilliseconds(300.5));
            metrics.RecordAnswer(TimeSpan.FromMilliseconds(500));
            metrics.RecordDictionarySearch(TimeSpan.FromMilliseconds(300));

            var output = metrics.ExportPrometheus();

            output.Should().Contain("# TYPE trainer_answer_duration_ms histogram\n")
                .And.Contain("trainer_answer_duration_ms_bucket{le=\"300\"} 0\n")
                .And.Contain("trainer_answer_duration_ms_bucket{le=\"500\"} 2\n")
                .And.Contain("trainer_answer_duration_ms_bucket{le=\"+Inf\"} 2\n")
                .And.Contain("trainer_answer_duration_ms_sum 800.5\n")
                .And.Contain("trainer_dictionary_search_duration_ms_bucket{le=\"300\"} 1\n")
                .And.Contain("trainer_today_ready_ms_bucket{le=\"1500\"} 1\n");
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public async Task Histograms_KeepConcurrentObservations()
    {
        var metrics = new TrainerMetrics();

        await Parallel.ForAsync(0, 1000, (_, _) =>
        {
            metrics.RecordTodayReady(1);
            return ValueTask.CompletedTask;
        });

        metrics.ExportPrometheus().Should().Contain("trainer_today_ready_ms_count 1000\n")
            .And.Contain("trainer_today_ready_ms_sum 1000\n")
            .And.Contain("trainer_today_ready_ms_bucket{le=\"10\"} 1000\n");
    }

    [Theory]
    [InlineData(-1)]
    public void Histograms_RejectInvalidDurations(double milliseconds)
    {
        var metrics = new TrainerMetrics();

        var action = () => metrics.RecordAnswer(TimeSpan.FromMilliseconds(milliseconds));

        action.Should().Throw<ArgumentException>();
        metrics.ExportPrometheus().Should().Contain("trainer_answer_duration_ms_count 0\n");
    }
}
