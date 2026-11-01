using System.Collections.Concurrent;
using System.Security.Claims;
using ConstantLearning.Features.Identity;
using ConstantLearning.Features.Identity.Auth;
using ConstantLearning.Features.Learning.Telemetry;
using ConstantLearning.Platform;
using ConstantLearning.Platform.Persistence;
using ConstantLearning.Tests.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NodaTime;

namespace ConstantLearning.Tests.Identity.Auth;

internal sealed class AuthTestApplication(WebApplication app, HttpClient client, CapturedAuthLogs logs) : IAsyncDisposable
{
    public HttpClient Client { get; } = client;
    public CapturedAuthLogs Logs { get; } = logs;

    public static async Task<AuthTestApplication> StartAsync(string connectionString, IClock clock, long? editorId = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var configuration = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = connectionString,
            ["Telegram:BotToken"] = TelegramInitDataSigner.TestBotToken
        };
        if (editorId is { } configuredEditor)
        {
            configuration["Identity:ContentEditorTelegramUserIds:0"] = configuredEditor.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        builder.Configuration.AddInMemoryCollection(configuration);
        var logs = new CapturedAuthLogs();
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Trace);
        builder.Logging.AddProvider(logs);
        builder.Services.AddSingleton(clock);
        builder.Services.AddPlatform(builder.Configuration);
        builder.Services.AddIdentity(builder.Configuration);
        builder.Services.AddControllers().AddApplicationPart(typeof(ClientTimingController).Assembly);

        var application = builder.Build();
        application.UseRouting();
        application.UseAuthentication();
        application.UseAuthorization();
        application.MapControllers();
        application.MapGet("/auth-test/me", (ClaimsPrincipal principal) => Results.Ok(new
        {
            learner_id = principal.GetLearnerId(),
            telegram_user_id = principal.FindFirstValue(TelegramAuthentication.TelegramUserIdClaim),
            first_name = principal.FindFirstValue(ClaimTypes.GivenName)
        })).RequireAuthorization();
        // Проба інтеграційного harness: реальні resource endpoints додадуть власні перевірки власника.
        application.MapGet("/auth-test/resources/{ownerId:guid}", async (Guid ownerId, ClaimsPrincipal principal, AppDbContext db) =>
        {
            var currentLearnerId = principal.GetLearnerId();
            var ownResource = await db.Learners.AsNoTracking()
                .Where(learner => learner.Id == currentLearnerId && learner.Id == ownerId)
                .Select(learner => new { learner.Id }).SingleOrDefaultAsync();
            return ownResource is null
                ? Results.NotFound(new { code = "session.item_not_found", message = "Картку не знайдено." })
                : Results.Ok(ownResource);
        }).RequireAuthorization();
        application.MapGet("/auth-test/editor/queue", () => Results.Ok()).RequireAuthorization(TelegramAuthentication.ContentEditorPolicy);
        application.MapGet("/auth-test/editor/changes", () => Results.Ok()).RequireAuthorization(TelegramAuthentication.ContentEditorPolicy);
        application.MapPost("/auth-test/editor/rollback", () => Results.Ok()).RequireAuthorization(TelegramAuthentication.ContentEditorPolicy);
        await application.StartAsync();
        return new AuthTestApplication(application, application.GetTestClient(), logs);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await app.DisposeAsync();
    }
}

internal sealed class CapturedAuthLogs : ILoggerProvider
{
    public ConcurrentQueue<string> Entries { get; } = new();

    public ILogger CreateLogger(string categoryName) => new CapturedLogger(categoryName, Entries);
    public void Dispose() { }

    private sealed class CapturedLogger(string category, ConcurrentQueue<string> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            entries.Enqueue($"{category}: scope: {state}");
            return null;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            entries.Enqueue($"{category}: {formatter(state, exception)} {exception}");
            if (state is IEnumerable<KeyValuePair<string, object?>> values)
            {
                foreach (var value in values)
                {
                    entries.Enqueue($"{value.Key}: {value.Value}");
                }
            }
        }
    }
}
