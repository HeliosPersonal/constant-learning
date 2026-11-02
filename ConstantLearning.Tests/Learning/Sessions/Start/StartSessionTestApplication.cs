using ConstantLearning.Features.Content;
using ConstantLearning.Features.Identity;
using ConstantLearning.Features.Learning;
using ConstantLearning.Features.Learning.Sessions.Start;
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

namespace ConstantLearning.Tests.Learning.Sessions.Start;

internal sealed class StartSessionTestApplication(WebApplication app, HttpClient client) : IAsyncDisposable
{
    public HttpClient Client { get; } = client;

    public static async Task<StartSessionTestApplication> StartAsync(string connectionString, IClock clock,
        IInterceptor? interceptor = null)
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
        if (interceptor is not null)
            builder.Services.AddDbContext<AppDbContext>(options => options.AddInterceptors(interceptor));
        builder.Services.AddControllers().AddApplicationPart(typeof(StartSessionController).Assembly);
        var application = builder.Build();
        application.UseRouting();
        application.UseAuthentication();
        application.UseAuthorization();
        application.MapControllers();
        await application.StartAsync();
        return new(application, application.GetTestClient());
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await app.DisposeAsync();
    }
}
