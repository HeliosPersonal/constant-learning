using System.Net;
using AwesomeAssertions;

namespace ConstantLearning.Tests.Platform.Metrics;

public class MetricsEndpointsTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    [InlineData("::ffff:127.0.0.1")]
    public async Task Metrics_AreAvailableToLocalDevelopmentWithoutAuthentication(string address)
    {
        await using var application = await MetricsTestApplication.StartAsync(address: address);

        using var response = await application.Client.GetAsync("/metrics");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.ToString().Should().Be("text/plain; version=0.0.4; charset=utf-8");
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await response.Content.ReadAsStringAsync()).Should().Contain("trainer_today_ready_ms_count 0\n")
            .And.Contain("trainer_answer_duration_ms_count 0\n")
            .And.Contain("trainer_dictionary_search_duration_ms_count 0\n");
    }

    [Fact]
    public async Task Metrics_RejectRemoteDevelopmentRequest()
    {
        await using var application = await MetricsTestApplication.StartAsync(address: "192.0.2.1");

        using var response = await application.Client.GetAsync("/metrics");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task Metrics_AreNotMappedOutsideDevelopment(string environment)
    {
        await using var application = await MetricsTestApplication.StartAsync(environment);
        application.Client.DefaultRequestHeaders.Authorization = new("tma", "private-init-data");

        using var response = await application.Client.GetAsync("/metrics");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ServerTiming_RecordsAnswerAndSearchWithoutQueryOrResourceLabels()
    {
        await using var application = await MetricsTestApplication.StartAsync();

        const string sessionId = "019a3e5e-7880-7000-8000-000000000001";
        const string itemId = "019a3e5e-7880-7000-8000-000000000002";
        using var answer = await application.Client.PostAsync($"/api/v1/sessions/{sessionId}/items/{itemId}/answer", null);
        using var search = await application.Client.GetAsync("/api/v1/dictionary/search?q=private-search");
        using var failedSearch = await application.Client.GetAsync("/api/v1/dictionary/search?fail=true");
        using var unrelated = await application.Client.GetAsync("/metrics");
        var output = await application.Client.GetStringAsync("/metrics");

        answer.StatusCode.Should().Be(HttpStatusCode.NoContent);
        search.StatusCode.Should().Be(HttpStatusCode.OK);
        failedSearch.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        output.Should().Contain("trainer_answer_duration_ms_count 1\n")
            .And.Contain("trainer_dictionary_search_duration_ms_count 2\n")
            .And.NotContain(sessionId).And.NotContain(itemId).And.NotContain("private-search");
    }
}
