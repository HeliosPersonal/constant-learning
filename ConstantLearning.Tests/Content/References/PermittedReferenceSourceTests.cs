using System.Net;
using ConstantLearning.Features.Content.References;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using NodaTime.Testing;

namespace ConstantLearning.Tests.Content.References;

public sealed class PermittedReferenceSourceTests
{
    [Fact]
    public async Task UnknownLemmaAndOtherFields_NeverMakeHttpRequest()
    {
        var handler = new StubHandler();
        using var client = new HttpClient(handler);
        using var source = new WsjpReferenceSource(client, new());
        (await source.LookupAsync(new("anything", "transcription"))).Reason.Should().Be("permitted_wsjp_page_missing");
        (await source.LookupAsync(new("anything", "translation"))).Reason.Should().Be("unsupported_field");
        handler.Calls.Should().Be(0);
    }

    [Fact]
    public async Task PermittedPage_IsBoundedAndCached_WithoutClaimingHtmlConfirmsIpa()
    {
        var handler = new StubHandler();
        using var client = new HttpClient(handler);
        using var source = new WsjpReferenceSource(client, Options());
        (await source.LookupAsync(new("kot", "transcription"))).Reason.Should().Be("wsjp_requires_editor");
        (await source.LookupAsync(new("KOT", "transcription"))).Reason.Should().Be("wsjp_requires_editor");
        handler.Calls.Should().Be(1);
    }

    [Fact]
    public async Task DistinctPermittedTerms_ShareOneRateLimit()
    {
        var options = Options();
        options.WsjpPages["dom"] = new("https://wsjp.pl/haslo/podglad/2/dom");
        var handler = new StubHandler();
        using var client = new HttpClient(handler);
        using var source = new WsjpReferenceSource(client, options);
        await source.LookupAsync(new("kot", "transcription"));
        (await source.LookupAsync(new("dom", "transcription"))).Reason.Should().Be("source_rate_limited");
        handler.Calls.Should().Be(1);
    }

    [Fact]
    public async Task OversizedBody_FallsBackWithoutParsing()
    {
        var options = Options();
        options.WsjpMaxResponseBytes = 10;
        var handler = new StubHandler { Response = () => new(HttpStatusCode.OK) { Content = new StringContent(new string('x', 100)) } };
        using var client = new HttpClient(handler);
        using var source = new WsjpReferenceSource(client, options);
        (await source.LookupAsync(new("kot", "transcription"))).Reason.Should().Be("response_too_large");
    }

    [Fact]
    public async Task StreamingBodyWithoutLength_IsAlsoBounded()
    {
        var options = Options();
        options.WsjpMaxResponseBytes = 10;
        var handler = new StubHandler
        {
            Response = () => new(HttpStatusCode.OK) { Content = new StreamContent(new NonSeekableStream(new byte[100])) }
        };
        using var client = new HttpClient(handler);
        using var source = new WsjpReferenceSource(client, options);
        (await source.LookupAsync(new("kot", "transcription"))).Reason.Should().Be("response_too_large");
    }

    [Fact]
    public async Task CallerCancellation_IsPropagated()
    {
        var handler = new StubHandler { Delay = true };
        using var client = new HttpClient(handler);
        using var source = new WsjpReferenceSource(client, Options());
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));
        var action = () => source.LookupAsync(new("kot", "transcription"), cancellation.Token);
        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task TimeoutAndHttpFailure_RequireEditor()
    {
        var options = Options();
        options.WsjpTimeout = TimeSpan.FromMilliseconds(30);
        var handler = new StubHandler { Delay = true };
        using var client = new HttpClient(handler);
        using var source = new WsjpReferenceSource(client, options);
        (await source.LookupAsync(new("kot", "transcription"))).Reason.Should().Be("source_timeout");
        (await source.LookupAsync(new("kot", "transcription"))).Reason.Should().Be("source_timeout");
        handler.Calls.Should().Be(1);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, "source_rate_limited")]
    [InlineData(HttpStatusCode.InternalServerError, "source_unavailable")]
    [InlineData(HttpStatusCode.Redirect, "source_unavailable")]
    public async Task HttpStatus_IsManualAndCached(HttpStatusCode status, string reason)
    {
        var handler = new StubHandler { Response = () => new(status) };
        using var client = new HttpClient(handler);
        using var source = new WsjpReferenceSource(client, Options());
        (await source.LookupAsync(new("kot", "transcription"))).Reason.Should().Be(reason);
        (await source.LookupAsync(new("kot", "transcription"))).Reason.Should().Be(reason);
        handler.Calls.Should().Be(1);
    }

    [Theory]
    [InlineData("http://wsjp.pl/haslo/1")]
    [InlineData("https://wsjp.pl.attacker.invalid/haslo/1")]
    [InlineData("https://user@wsjp.pl/haslo/1")]
    [InlineData("https://wsjp.pl:8443/haslo/1")]
    public async Task NonPermittedHost_NeverMakesHttpRequest(string url)
    {
        var options = Options();
        options.WsjpPages["kot"] = new(url);
        var handler = new StubHandler();
        using var client = new HttpClient(handler);
        using var source = new WsjpReferenceSource(client, options);
        (await source.LookupAsync(new("kot", "transcription"))).Reason.Should().Be("permitted_wsjp_page_missing");
        handler.Calls.Should().Be(0);
    }

    [Fact]
    public async Task GovernmentReference_RequiresExplicitReviewedMapping()
    {
        var clock = new FakeClock(Instant.FromUtc(2026, 11, 1, 0, 0));
        var options = new ReferenceSourcesOptions();
        var source = new GovernmentReferenceSource(options, clock);
        (await source.LookupAsync(new("pobyt", "translation"))).Status.Should().Be(ReferenceStatus.ManualReview);
        options.GovernmentReferences.Add(new("pobyt", "перебування", new("https://www.gov.pl/web/udsc"),
            clock.GetCurrentInstant(), "Редактор перевірив український термін; тестовий зразок."));
        (await source.LookupAsync(new("pobyt", "translation"))).Status.Should().Be(ReferenceStatus.Available);
        clock.Advance(Duration.FromDays(36));
        (await source.LookupAsync(new("pobyt", "translation"))).Status.Should().Be(ReferenceStatus.ManualReview);
    }

    [Fact]
    public void Registration_UsesSingletonHttpSourceAndDoesNotPerformIo()
    {
        using var provider = new ServiceCollection().AddReferenceSources().BuildServiceProvider();
        provider.GetRequiredService<WsjpReferenceSource>().Should().BeSameAs(provider.GetRequiredService<WsjpReferenceSource>());
    }

    private static ReferenceSourcesOptions Options()
    {
        var options = new ReferenceSourcesOptions();
        options.WsjpPages["kot"] = new("https://wsjp.pl/haslo/podglad/1/kot");
        return options;
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public bool Delay { get; init; }
        public Func<HttpResponseMessage> Response { get; init; } = () => new(HttpStatusCode.OK) { Content = new StringContent("<html>test</html>") };
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            if (Delay) await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return Response();
        }
    }

    private sealed class NonSeekableStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
    }
}
