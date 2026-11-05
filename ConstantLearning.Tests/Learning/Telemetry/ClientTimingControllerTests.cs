using System.Net;
using System.Text;
using ConstantLearning.Tests.Infrastructure;
using ConstantLearning.Tests.Platform.Metrics;
using FluentAssertions;

namespace ConstantLearning.Tests.Learning.Telemetry;

public class ClientTimingControllerTests
{
    [Theory]
    [InlineData("0", "0")]
    [InlineData("840", "840")]
    [InlineData("60000", "60000")]
    [InlineData("1.0", "1")]
    [InlineData("1e2", "100")]
    [InlineData("-0e99999999999999999999", "0")]
    public async Task ReportClientTiming_RecordsContractIntegerAndReturnsEmpty204(string value, string sum)
    {
        await using var application = await MetricsTestApplication.StartAsync();
        application.Client.DefaultRequestHeaders.Authorization = new("tma", "private-init-data");

        using var response = await PostAsync(application.Client, $$"""{"metric":"today_ready_ms","value_ms":{{value}}}""");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await ContractAssert.ResponseAsync("reportClientTiming", response);
        var metrics = await application.Client.GetStringAsync("/metrics");
        metrics.Should().Contain("trainer_today_ready_ms_count 1\n").And.Contain($"trainer_today_ready_ms_sum {sum}\n");
        metrics.Should().NotContain("987654321").And.NotContain("private-init-data").And.NotContain("Authorization");
    }

    [Theory]
    [InlineData("{\"metric\":\"other\",\"value_ms\":840}")]
    [InlineData("{\"metric\":\"TODAY_READY_MS\",\"value_ms\":840}")]
    [InlineData("{\"metric\":\"today_ready_ms\",\"value_ms\":-1}")]
    [InlineData("{\"metric\":\"today_ready_ms\",\"value_ms\":60001}")]
    [InlineData("{\"metric\":\"today_ready_ms\",\"value_ms\":1.5}")]
    [InlineData("{\"metric\":\"today_ready_ms\",\"value_ms\":1.000000000000000000000000000001}")]
    [InlineData("{\"metric\":\"today_ready_ms\",\"value_ms\":0.000000000000000000000000000001}")]
    [InlineData("{\"metric\":\"today_ready_ms\",\"value_ms\":60000.00000000000000000000000001}")]
    [InlineData("{\"metric\":\"today_ready_ms\",\"value_ms\":1e400}")]
    [InlineData("{\"metric\":\"today_ready_ms\",\"value_ms\":\"840\"}")]
    [InlineData("{\"metric\":\"today_ready_ms\",\"value_ms\":null}")]
    [InlineData("{\"metric\":null,\"value_ms\":840}")]
    [InlineData("{\"metric\":7,\"value_ms\":840}")]
    [InlineData("{\"metric\":\"today_ready_ms\"}")]
    [InlineData("{\"value_ms\":840}")]
    [InlineData("{\"metric\":\"today_ready_ms\",\"value_ms\":840,\"telegram_id\":987654321}")]
    [InlineData("{\"Metric\":\"today_ready_ms\",\"value_ms\":840}")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{")]
    [InlineData("")]
    public async Task ReportClientTiming_RejectsInvalidBodyWithContractError(string body)
    {
        await using var application = await MetricsTestApplication.StartAsync();
        application.Client.DefaultRequestHeaders.Authorization = new("tma", "private-init-data");

        using var response = await PostAsync(application.Client, body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ContractAssert.ResponseAsync("reportClientTiming", response);
        (await response.Content.ReadAsStringAsync()).Should().Contain("telemetry.invalid_metric");
        application.Metrics.ExportPrometheus().Should().Contain("trainer_today_ready_ms_count 0\n");
    }

    [Fact]
    public async Task ReportClientTiming_RecordsLanguageSwitchMetricSeparately()
    {
        await using var application = await MetricsTestApplication.StartAsync();
        application.Client.DefaultRequestHeaders.Authorization = new("tma", "private-init-data");

        using var response = await PostAsync(application.Client, """{"metric":"language_switch_ms","value_ms":620}""");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await ContractAssert.ResponseAsync("reportClientTiming", response);
        var metrics = await application.Client.GetStringAsync("/metrics");
        metrics.Should().Contain("trainer_language_switch_ms_count 1\n").And.Contain("trainer_language_switch_ms_sum 620\n");
        // Перемикання мови не підмішується в метрику відкриття «Сьогодні» — гістограми окремі.
        metrics.Should().Contain("trainer_today_ready_ms_count 0\n");
    }

    [Fact]
    public async Task ReportClientTiming_RequiresAuthenticationBeforeRecording()
    {
        await using var application = await MetricsTestApplication.StartAsync();

        using var response = await PostAsync(application.Client, """{"metric":"today_ready_ms","value_ms":840}""");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        application.Metrics.ExportPrometheus().Should().Contain("trainer_today_ready_ms_count 0\n");
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string body) =>
        client.PostAsync("/api/v1/telemetry/client-timings", new StringContent(body, Encoding.UTF8, "application/json"));
}
