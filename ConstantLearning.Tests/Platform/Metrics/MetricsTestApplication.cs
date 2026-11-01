using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using ConstantLearning.Features.Learning.Telemetry;
using ConstantLearning.Platform;
using ConstantLearning.Platform.Metrics;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConstantLearning.Tests.Platform.Metrics;

internal sealed class MetricsTestApplication(WebApplication app, HttpClient client) : IAsyncDisposable
{
    public HttpClient Client { get; } = client;
    public TrainerMetrics Metrics => app.Services.GetRequiredService<TrainerMetrics>();

    public static async Task<MetricsTestApplication> StartAsync(string environment = "Development", string address = "127.0.0.1")
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=unused;Username=unused;Password=unused"
        });
        builder.Services.AddPlatform(builder.Configuration);
        builder.Services.AddControllers().AddApplicationPart(typeof(ClientTimingController).Assembly)
            .ConfigureApplicationPartManager(manager => manager.FeatureProviders.Add(new TelemetryControllerFeatureProvider()));
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
        builder.Services.AddAuthorization(options => options.FallbackPolicy = options.DefaultPolicy);

        var application = builder.Build();
        application.Use((context, next) =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(address);
            return next(context);
        });
        application.UseRouting();
        application.UseMiddleware<ServerTimingMiddleware>();
        application.UseAuthentication();
        application.UseAuthorization();
        application.MapControllers();
        application.MapLocalMetrics();
        application.MapPost("/api/v1/sessions/{sessionId:guid}/items/{itemId:guid}/answer", () => Results.NoContent()).AllowAnonymous();
        application.MapGet("/api/v1/dictionary/search", (HttpContext context) =>
            context.Request.Query.ContainsKey("fail") ? Results.StatusCode(500) : Results.Ok()).AllowAnonymous();
        await application.StartAsync();
        return new MetricsTestApplication(application, application.GetTestClient());
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await app.DisposeAsync();
    }

    private sealed class TelemetryControllerFeatureProvider : IApplicationFeatureProvider<ControllerFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
        {
            // Harness перевіряє лише telemetry; решта маршрутів тут є явними пробами метрик.
            for (var index = feature.Controllers.Count - 1; index >= 0; index--)
            {
                if (feature.Controllers[index].AsType() != typeof(ClientTimingController))
                {
                    feature.Controllers.RemoveAt(index);
                }
            }
        }
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.Authorization.Contains("tma private-init-data"))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "987654321")], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
