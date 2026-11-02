using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using ConstantLearning.HostedServices;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NodaTime;

[assembly: WebApplicationFactoryContentRoot("ConstantLearning, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null", "/app", "ConstantLearning.dll", "0")]

if (args is ["--probe", var probeUrl])
{
    using var probeClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    var response = await probeClient.GetAsync(probeUrl);
    response.EnsureSuccessStatusCode();
    var body = await response.Content.ReadAsStringAsync();
    if (!body.Contains("\"status\":\"healthy\"", StringComparison.Ordinal))
        throw new InvalidOperationException("Probe не повернув healthy.");
    Console.WriteLine($"PROBE_OK={probeUrl}");
    return;
}

// Цей helper існує лише у Docker target container-smoke та не змінює production startup.
Environment.SetEnvironmentVariable("Telegram__BotToken", "123456789:LOCAL_TEST_TOKEN_NO_TELEGRAM_MESSAGES");
Environment.SetEnvironmentVariable("Telegram__WebhookUrl", "");
Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Testing");

var polish = CultureInfo.GetCultureInfo("pl-PL");
if ("ŁÓDŹ".ToLower(polish) != "łódź" || polish.DateTimeFormat.GetMonthName(1) != "styczeń")
    throw new InvalidOperationException("ICU не забезпечує польську культуру.");
foreach (var (id, winterHours, summerHours) in new[] { ("Europe/Warsaw", 1, 2), ("Asia/Tokyo", 9, 9) })
{
    var system = TimeZoneInfo.FindSystemTimeZoneById(id);
    var noda = DateTimeZoneProviders.Tzdb[id];
    foreach (var (month, hours) in new[] { (1, winterHours), (7, summerHours) })
    {
        var instant = Instant.FromUtc(2026, month, 15, 12, 0);
        if (system.GetUtcOffset(instant.ToDateTimeUtc()) != TimeSpan.FromHours(hours)
            || noda.GetUtcOffset(instant).ToTimeSpan() != TimeSpan.FromHours(hours))
            throw new InvalidOperationException($"Хибний UTC offset для {id} у місяці {month}.");
    }
    Console.WriteLine($"TIMEZONE_OK={id}; winter={winterHours}; summer={summerHours}");
}
Console.WriteLine("ICU_OK=pl-PL");

await using var factory = new ContainerFactory();
factory.UseKestrel(options => options.ListenAnyIP(8888));
using var client = factory.CreateClient();
client.BaseAddress = new Uri("http://127.0.0.1:8888");
var health = await client.GetAsync("/api/telegram/health");
health.EnsureSuccessStatusCode();
var html = await client.GetStringAsync("/");
if (!html.Contains("id=\"root\"", StringComparison.Ordinal))
    throw new InvalidOperationException("В образі немає оболонки Mini App.");
var asset = Regex.Match(html, "src=\"(/assets/[^\"]+\\.js)\"").Groups[1].Value;
if (string.IsNullOrEmpty(asset)) throw new InvalidOperationException("В оболонці немає JS-ресурсу.");
(await client.GetAsync(asset)).EnsureSuccessStatusCode();
if (await client.GetStringAsync("/settings/appearance") != html
    || (await client.GetAsync("/api/v1/container-smoke-unknown")).StatusCode != HttpStatusCode.NotFound)
    throw new InvalidOperationException("Помилка SPA fallback або API 404.");
Console.WriteLine("CONTAINER_SMOKE_READY=8888");
await Task.Delay(Timeout.InfiniteTimeSpan);

sealed class ContainerFactory : WebApplicationFactory<ConstantLearning.Platform.Persistence.AppDbContext>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing").UseContentRoot("/app").UseWebRoot("/app/wwwroot");
        builder.ConfigureServices(services =>
        {
            // Реальні міграції збережено, а webhook і планувальник зовнішніх відправлень вимкнено.
            var externalServices = services.Where(service => service.ServiceType == typeof(IHostedService)
                && (service.ImplementationType == typeof(WebhookConfigurationService)
                    || service.ImplementationType?.Namespace?.StartsWith("Quartz", StringComparison.Ordinal) == true)).ToArray();
            foreach (var service in externalServices) services.Remove(service);
        });
    }
}
