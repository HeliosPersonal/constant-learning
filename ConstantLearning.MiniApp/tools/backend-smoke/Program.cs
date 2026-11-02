using System.Net;
using System.Text.RegularExpressions;
using ConstantLearning.HostedServices;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

// Цей локальний host запускає справжній Program і PostgreSQL без зовнішніх відправлень.
await using var database = new PostgreSqlBuilder("postgres:16-alpine")
    .WithDatabase("miniapp_smoke")
    .WithUsername("miniapp_test")
    .WithPassword(Guid.NewGuid().ToString("N"))
    .Build();
await database.StartAsync();

Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", database.GetConnectionString());
Environment.SetEnvironmentVariable("Telegram__BotToken", "123456789:LOCAL_TEST_TOKEN_NO_TELEGRAM_MESSAGES");
Environment.SetEnvironmentVariable("Telegram__WebhookUrl", "");
Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Testing");

await using var factory = new MiniAppFactory();
factory.UseKestrel(0);
using var client = factory.CreateClient();
var root = await client.GetAsync("/");
root.EnsureSuccessStatusCode();
var html = await root.Content.ReadAsStringAsync();
if (root.Content.Headers.ContentType?.MediaType != "text/html" || !html.Contains("id=\"root\""))
    throw new InvalidOperationException("Кореневий маршрут не повернув оболонку Mini App.");
var deepLink = await client.GetStringAsync("/settings/appearance");
if (deepLink != html) throw new InvalidOperationException("SPA fallback не повернув index.html.");
var unknownApi = await client.GetAsync("/api/v1/unknown-miniapp-smoke");
if (unknownApi.StatusCode != HttpStatusCode.NotFound)
    throw new InvalidOperationException("Невідомий API-маршрут повинен повернути 404.");
var assetPath = Regex.Match(html, "src=\"(/assets/[^\"]+\\.js)\"").Groups[1].Value;
if (string.IsNullOrWhiteSpace(assetPath)) throw new InvalidOperationException("Не знайдено зібраний JS-ресурс.");
(await client.GetAsync(assetPath)).EnsureSuccessStatusCode();
Console.WriteLine($"MINIAPP_BASE_URL={client.BaseAddress!.ToString().TrimEnd('/')}");
Console.WriteLine("Перевірено: PostgreSQL 16, кореневий SPA, вкладений маршрут, JS-ресурс і API 404.");
if (!args.Contains("--once"))
{
    Console.WriteLine("Натисніть Enter після браузерної перевірки: host і контейнер буде зупинено.");
    Console.ReadLine();
}

sealed class MiniAppFactory : WebApplicationFactory<ConstantLearning.Platform.Persistence.AppDbContext>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var backend = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../ConstantLearning"));
        builder.UseEnvironment("Testing").UseContentRoot(backend).UseWebRoot(Path.Combine(backend, "wwwroot"));
        builder.ConfigureServices(services =>
        {
            // Webhook змінює зовнішній Telegram, а Quartz запускає надсилання за розкладом.
            var externalServices = services.Where(service => service.ServiceType == typeof(IHostedService)
                && (service.ImplementationType == typeof(WebhookConfigurationService)
                    || service.ImplementationType?.Namespace?.StartsWith("Quartz", StringComparison.Ordinal) == true)).ToArray();
            foreach (var service in externalServices) services.Remove(service);
        });
    }
}
