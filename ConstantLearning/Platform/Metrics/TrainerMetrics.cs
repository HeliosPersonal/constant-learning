using System.Globalization;
using System.Text;

namespace ConstantLearning.Platform.Metrics;

public sealed class TrainerMetrics
{
    private readonly TimingHistogram _todayReady = new("trainer_today_ready_ms", "Час відкриття екрана Сьогодні в клієнті.");
    private readonly TimingHistogram _answer = new("trainer_answer_duration_ms", "Час HTTP-запиту оцінки відповіді.");
    private readonly TimingHistogram _search = new("trainer_dictionary_search_duration_ms", "Час HTTP-запиту пошуку в словнику.");
    private readonly LabeledCounter _progressReset = new("trainer_progress_reset_total",
        "Підсумки скидань прогресу learner (без персональних даних).", "outcome");
    private readonly TimingHistogram _progressResetDuration = new("trainer_progress_reset_duration_ms",
        "Тривалість скидання прогресу learner.");
    private readonly TimingHistogram _extraWordStart = new("trainer_extra_word_start_ms",
        "Тривалість старту додаткового слова.");
    private readonly TimingHistogram _extraPracticeStart = new("trainer_extra_practice_start_ms",
        "Тривалість старту додаткової практики.");
    private readonly TimingHistogram _statsQuery = new("trainer_stats_query_ms",
        "Тривалість запиту статистики вивчених слів.");

    public void RecordTodayReady(int milliseconds) => _todayReady.Record(milliseconds);

    public void RecordAnswer(TimeSpan duration) => _answer.Record(duration.TotalMilliseconds);

    public void RecordDictionarySearch(TimeSpan duration) => _search.Record(duration.TotalMilliseconds);

    public void RecordProgressReset(string outcome) => _progressReset.Increment(outcome);

    public void RecordProgressResetDuration(TimeSpan duration) => _progressResetDuration.Record(duration.TotalMilliseconds);

    public void RecordExtraWordStart(TimeSpan duration) => _extraWordStart.Record(duration.TotalMilliseconds);

    public void RecordExtraPracticeStart(TimeSpan duration) => _extraPracticeStart.Record(duration.TotalMilliseconds);

    public void RecordStatsQuery(TimeSpan duration) => _statsQuery.Record(duration.TotalMilliseconds);

    public string ExportPrometheus()
    {
        var output = new StringBuilder();
        _todayReady.AppendTo(output);
        _answer.AppendTo(output);
        _search.AppendTo(output);
        _progressReset.AppendTo(output);
        _progressResetDuration.AppendTo(output);
        _extraWordStart.AppendTo(output);
        _extraPracticeStart.AppendTo(output);
        _statsQuery.AppendTo(output);
        return output.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private sealed class LabeledCounter(string name, string description, string label)
    {
        private readonly object _gate = new();
        private readonly Dictionary<string, long> _values = new(StringComparer.Ordinal);

        public void Increment(string value)
        {
            ArgumentException.ThrowIfNullOrEmpty(value);
            lock (_gate)
            {
                _values[value] = _values.TryGetValue(value, out var count) ? count + 1 : 1;
            }
        }

        public void AppendTo(StringBuilder output)
        {
            lock (_gate)
            {
                output.Append("# HELP ").Append(name).Append(' ').AppendLine(description);
                output.Append("# TYPE ").Append(name).AppendLine(" counter");
                foreach (var (value, count) in _values.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    output.Append(name).Append('{').Append(label).Append("=\"").Append(value).Append("\"} ")
                        .AppendLine(count.ToString(CultureInfo.InvariantCulture));
                }
            }
        }
    }

    private sealed class TimingHistogram(string name, string description)
    {
        private static readonly double[] Bounds = [10, 25, 50, 100, 200, 300, 500, 750, 1000, 1500, 2500, 5000, 10000, 30000, 60000];
        private readonly object _gate = new();
        private readonly long[] _buckets = new long[Bounds.Length];
        private long _count;
        private double _sum;

        public void Record(double milliseconds)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(milliseconds);
            if (!double.IsFinite(milliseconds))
            {
                throw new ArgumentOutOfRangeException(nameof(milliseconds), "Тривалість повинна бути скінченною.");
            }

            lock (_gate)
            {
                for (var index = 0; index < Bounds.Length; index++)
                {
                    if (milliseconds <= Bounds[index])
                    {
                        _buckets[index]++;
                    }
                }

                _count++;
                _sum += milliseconds;
            }
        }

        public void AppendTo(StringBuilder output)
        {
            lock (_gate)
            {
                output.Append("# HELP ").Append(name).Append(' ').AppendLine(description);
                output.Append("# TYPE ").Append(name).AppendLine(" histogram");
                for (var index = 0; index < Bounds.Length; index++)
                {
                    output.Append(name).Append("_bucket{le=\"")
                        .Append(Bounds[index].ToString(CultureInfo.InvariantCulture)).Append("\"} ")
                        .AppendLine(_buckets[index].ToString(CultureInfo.InvariantCulture));
                }

                output.Append(name).Append("_bucket{le=\"+Inf\"} ")
                    .AppendLine(_count.ToString(CultureInfo.InvariantCulture));
                output.Append(name).Append("_sum ").AppendLine(_sum.ToString("R", CultureInfo.InvariantCulture));
                output.Append(name).Append("_count ").AppendLine(_count.ToString(CultureInfo.InvariantCulture));
            }
        }
    }
}
