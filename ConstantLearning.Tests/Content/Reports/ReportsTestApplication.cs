using ConstantLearning.Features.Content;
using ConstantLearning.Features.Content.Reports;
using ConstantLearning.Features.Identity;
using ConstantLearning.Features.Learning;
using ConstantLearning.Platform;
using ConstantLearning.Tests.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;

namespace ConstantLearning.Tests.Content.Reports;

internal sealed class ReportsTestApplication(WebApplication app, HttpClient client) : IAsyncDisposable
{
    public HttpClient Client { get; } = client;
    public IServiceProvider Services => app.Services;

    public static async Task<ReportsTestApplication> StartAsync(string connectionString, IClock clock,
        Action<IServiceCollection>? configure = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
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
        builder.Services.AddControllers().AddApplicationPart(typeof(ContentReportsController).Assembly);
        configure?.Invoke(builder.Services);
        var application = builder.Build();
        application.UseRouting();
        application.UseAuthentication();
        application.UseAuthorization();
        application.MapControllers();
        await application.StartAsync();
        return new ReportsTestApplication(application, application.GetTestClient());
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await app.DisposeAsync();
    }
}
