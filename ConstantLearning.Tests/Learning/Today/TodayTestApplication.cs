using System.Collections.Concurrent;
using System.Diagnostics;
using ConstantLearning.Features.Content;
using ConstantLearning.Features.Learning.Today;
using ConstantLearning.Features.Identity;
using ConstantLearning.Features.Learning;
using ConstantLearning.Platform;
using ConstantLearning.Platform.Persistence;
using ConstantLearning.Tests.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NodaTime;

namespace ConstantLearning.Tests.Learning.Today;

internal sealed class TodayTestApplication(WebApplication app, HttpClient client,
    ConcurrentQueue<double> serverMilliseconds) : IAsyncDisposable
{
    public HttpClient Client { get; } = client;
    public ConcurrentQueue<double> ServerMilliseconds { get; } = serverMilliseconds;
    public IServiceProvider Services => app.Services;

    public static async Task<TodayTestApplication> StartAsync(string connectionString, IClock clock,
        DbCommandInterceptor? interceptor = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = connectionString,
            ["Telegram:BotToken"] = TelegramInitDataSigner.TestBotToken
        });
        builder.Services.AddSingleton(clock);
        builder.Services.AddPlatform(builder.Configuration);
        builder.Services.AddIdentity(builder.Configuration);
        builder.Services.AddLearning();
        builder.Services.AddContent();
        builder.Services.AddControllers().AddApplicationPart(typeof(TodayController).Assembly);
        if (interceptor is not null) builder.Services.AddDbContext<AppDbContext>(options => options.AddInterceptors(interceptor));
        var application = builder.Build();
        var measurements = new ConcurrentQueue<double>();
        application.Use(async (context, next) =>
        {
            var start = Stopwatch.GetTimestamp();
            try { await next(context); }
            finally { measurements.Enqueue(Stopwatch.GetElapsedTime(start).TotalMilliseconds); }
        });
        application.UseRouting();
        application.UseAuthentication();
        application.UseAuthorization();
        application.MapControllers();
        await application.StartAsync();
        return new(application, application.GetTestClient(), measurements);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await app.DisposeAsync();
    }
}
