using System.Net;
using ConstantLearning.Configuration;
using ConstantLearning.Platform.Telegram;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ConstantLearning.Tests.Notifications.Dispatch;

public sealed class TelegramDeliveryAdapterTests
{
    [Theory]
    [InlineData(429, "Too Many Requests", TelegramDeliveryFailure.RejectedTransient)]
    [InlineData(400, "Bad Request", TelegramDeliveryFailure.RejectedPermanent)]
    [InlineData(403, "Forbidden: bot was blocked by the user", TelegramDeliveryFailure.BotBlocked)]
    [InlineData(403, "Forbidden: bot is not a member", TelegramDeliveryFailure.RejectedPermanent)]
    [InlineData(500, "Internal Server Error", TelegramDeliveryFailure.Ambiguous)]
    public async Task ApiErrors_AreClassifiedWithoutLeakingTelegramDetails(int status, string description,
        TelegramDeliveryFailure expected)
    {
        var transport = new StubTransport(() => new HttpResponseMessage((HttpStatusCode)status)
        {
            Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(new
            {
                ok = false, error_code = status, description = description + " SECRET",
                parameters = new { retry_after = 120 }
            }))
        });
        await using var provider = CreateProvider(transport);
        var send = () => provider.GetRequiredService<ITelegramClient>().SendMessageAsync(123, "Тест");
        var failure = (await send.Should().ThrowAsync<TelegramDeliveryException>()).Which;
        failure.Failure.Should().Be(expected);
        failure.RetryAfterSeconds.Should().Be(120);
        failure.ToString().Should().NotContain("SECRET");
        failure.InnerException.Should().BeNull();
        transport.Calls.Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TransportAndTimeoutFailures_AreAmbiguousAndNotRetried(bool timeout)
    {
        var transport = new StubTransport(() => throw (timeout
            ? new TaskCanceledException("SECRET")
            : new HttpRequestException("SECRET")));
        await using var provider = CreateProvider(transport);
        var send = () => provider.GetRequiredService<ITelegramClient>().SendMessageAsync(123, "Тест");
        var failure = (await send.Should().ThrowAsync<TelegramDeliveryException>()).Which;
        failure.Failure.Should().Be(TelegramDeliveryFailure.Ambiguous);
        failure.ToString().Should().NotContain("SECRET");
        transport.Calls.Should().Be(1);
    }

    private static ServiceProvider CreateProvider(StubTransport transport)
    {
        var services = new ServiceCollection();
        services.Configure<TelegramOptions>(options => options.BotToken = "123456:SECRET");
        services.AddTelegram();
        services.AddHttpClient("Telegram").ConfigurePrimaryHttpMessageHandler(() => transport);
        return services.BuildServiceProvider();
    }

    private sealed class StubTransport(Func<HttpResponseMessage> response) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(response());
        }
    }
}
