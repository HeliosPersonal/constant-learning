using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using ConstantLearning.Features.Content.Search.Forms;
using NodaTime;

namespace ConstantLearning.Features.Content.References;

public sealed class ReferenceSourcesOptions
{
    /// <summary>Тільки попередньо дозволені сторінки окремих лем; адаптер не будує пошукових URL.</summary>
    public Dictionary<string, Uri> WsjpPages { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<GovernmentReference> GovernmentReferences { get; } = [];
    public TimeSpan WsjpMinimumInterval { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan WsjpTimeout { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan WsjpCacheLifetime { get; set; } = TimeSpan.FromHours(24);
    public int WsjpMaxResponseBytes { get; set; } = 256 * 1024;
}

/// <summary>Редактор явно пов'язує лему з українським терміном і конкретним урядовим джерелом.</summary>
public sealed record GovernmentReference(string Lemma, string Value, Uri Url, Instant ReviewedAt, string Attribution);

public sealed class GovernmentReferenceSource(ReferenceSourcesOptions options, IClock clock) : IReferenceSource
{
    public string Name => "government";
    public Task<ReferenceResult> LookupAsync(ReferenceQuery query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (query.Field != ReferenceFields.Translation) return Task.FromResult(ReferenceResult.Manual("unsupported_field"));
        if (string.IsNullOrWhiteSpace(query.Lemma) || query.Lemma.Length > 200)
            return Task.FromResult(ReferenceResult.Manual("invalid_lemma"));
        var now = clock.GetCurrentInstant();
        var records = options.GovernmentReferences.Where(reference =>
            SearchTextNormalizer.Normalize(reference.Lemma) == SearchTextNormalizer.Normalize(query.Lemma) &&
            IsGovernmentUrl(reference.Url) && !string.IsNullOrWhiteSpace(reference.Value) && reference.Value.Length <= 500 &&
            !string.IsNullOrWhiteSpace(reference.Attribution) && reference.ReviewedAt <= now &&
            now - reference.ReviewedAt <= Duration.FromDays(35)).ToArray();
        return Task.FromResult(records.Length == 0 ? ReferenceResult.Manual("permitted_government_reference_missing") :
            new ReferenceResult(ReferenceStatus.Available, records.Select(reference => new ReferenceCandidate(reference.Value,
                Name, reference.Url.AbsoluteUri, null, reference.ReviewedAt, reference.Attribution, null)).ToArray(), null));
    }

    private static bool IsGovernmentUrl(Uri uri) => uri.IsAbsoluteUri && uri.Scheme == "https" && uri.IsDefaultPort &&
        uri.UserInfo.Length == 0 && (uri.Host == "gov.pl" || uri.Host.EndsWith(".gov.pl", StringComparison.OrdinalIgnoreCase));
}

/// <summary>Точкове отримання дозволеної сторінки; її HTML сам по собі не підтверджує IPA чи переклад.</summary>
public sealed class WsjpReferenceSource(HttpClient client, ReferenceSourcesOptions options) : IReferenceSource, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ConcurrentDictionary<string, (long At, ReferenceResult Result)> _cache = new();
    private long? _lastRequest;
    public string Name => "wsjp";

    public async Task<ReferenceResult> LookupAsync(ReferenceQuery query, CancellationToken cancellationToken = default)
    {
        if (query.Field != ReferenceFields.Transcription) return ReferenceResult.Manual("unsupported_field");
        if (string.IsNullOrWhiteSpace(query.Lemma) || query.Lemma.Length > 200) return ReferenceResult.Manual("invalid_lemma");
        var key = SearchTextNormalizer.Normalize(query.Lemma);
        if (!options.WsjpPages.TryGetValue(key, out var url) || !IsWsjpUrl(url))
            return ReferenceResult.Manual("permitted_wsjp_page_missing");
        if (Cached(key) is { } cached) return cached;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.WsjpTimeout);
        var entered = false;
        try
        {
            await _gate.WaitAsync(timeout.Token);
            entered = true;
            if (Cached(key) is { } repeated) return repeated;
            if (_lastRequest is { } previous && Stopwatch.GetElapsedTime(previous) < options.WsjpMinimumInterval)
                return ReferenceResult.Manual("source_rate_limited");
            _lastRequest = Stopwatch.GetTimestamp();
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd("ConstantLearning/1.0 (reference-point-query)");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            ReferenceResult result;
            if (!response.IsSuccessStatusCode) result = ReferenceResult.Manual(response.StatusCode == HttpStatusCode.TooManyRequests
                ? "source_rate_limited" : "source_unavailable");
            else if (response.Content.Headers.ContentLength > options.WsjpMaxResponseBytes)
                result = ReferenceResult.Manual("response_too_large");
            else
            {
                await using var body = await response.Content.ReadAsStreamAsync(timeout.Token);
                var buffer = new byte[8192];
                var size = 0;
                int read;
                while ((read = await body.ReadAsync(buffer, timeout.Token)) > 0)
                {
                    size += read;
                    if (size > options.WsjpMaxResponseBytes) break;
                }
                result = ReferenceResult.Manual(size > options.WsjpMaxResponseBytes ? "response_too_large" : "wsjp_requires_editor");
            }
            // Кеш обмежений переліком дозволених лем; помилки також не породжують повторних запитів.
            _cache[key] = (Stopwatch.GetTimestamp(), result);
            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var result = ReferenceResult.Manual("source_timeout");
            _cache[key] = (Stopwatch.GetTimestamp(), result);
            return result;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            var result = ReferenceResult.Manual("source_unavailable");
            _cache[key] = (Stopwatch.GetTimestamp(), result);
            return result;
        }
        finally { if (entered) _gate.Release(); }
    }

    private ReferenceResult? Cached(string key) => _cache.TryGetValue(key, out var item) &&
        Stopwatch.GetElapsedTime(item.At) < options.WsjpCacheLifetime ? item.Result : null;
    private static bool IsWsjpUrl(Uri uri) => uri.IsAbsoluteUri && uri.Scheme == "https" && uri.IsDefaultPort &&
        uri.UserInfo.Length == 0 && uri.Host.Equals("wsjp.pl", StringComparison.OrdinalIgnoreCase);
    public void Dispose()
    {
        _gate.Dispose();
        client.Dispose();
    }
}
