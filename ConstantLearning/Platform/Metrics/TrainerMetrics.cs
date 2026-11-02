using System.Globalization;
using System.Text;

namespace ConstantLearning.Platform.Metrics;

public sealed class TrainerMetrics
{
    private readonly TimingHistogram _todayReady = new("trainer_today_ready_ms", "Час відкриття екрана Сьогодні в клієнті.");
    private readonly TimingHistogram _answer = new("trainer_answer_duration_ms", "Час HTTP-запиту оцінки відповіді.");
    private readonly TimingHistogram _search = new("trainer_dictionary_search_duration_ms", "Час HTTP-запиту пошуку в словнику.");

    public void RecordTodayReady(int milliseconds) => _todayReady.Record(milliseconds);

    public void RecordAnswer(TimeSpan duration) => _answer.Record(duration.TotalMilliseconds);

    public void RecordDictionarySearch(TimeSpan duration) => _search.Record(duration.TotalMilliseconds);

    public string ExportPrometheus()
    {
        var output = new StringBuilder();
        _todayReady.AppendTo(output);
        _answer.AppendTo(output);
        _search.AppendTo(output);
        return output.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
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
