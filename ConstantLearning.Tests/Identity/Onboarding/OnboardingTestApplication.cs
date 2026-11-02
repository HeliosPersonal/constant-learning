using ConstantLearning.Features.Identity;
using ConstantLearning.Features.Identity.Learners;
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
using NodaTime;

namespace ConstantLearning.Tests.Identity.Onboarding;

internal sealed class OnboardingTestApplication(WebApplication app, HttpClient client) : IAsyncDisposable
{
    public HttpClient Client { get; } = client;

    public static async Task<OnboardingTestApplication> StartAsync(string connectionString, IClock clock,
        IInterceptor? interceptor = null)
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
        if (interceptor is not null)
        {
            builder.Services.AddDbContext<AppDbContext>(options => options.AddInterceptors(interceptor));
        }

        builder.Services.AddControllers().AddApplicationPart(typeof(LearnersController).Assembly);
        var application = builder.Build();
        application.UseRouting();
        application.UseAuthentication();
        application.UseAuthorization();
        application.MapControllers();
        await application.StartAsync();
        return new OnboardingTestApplication(application, application.GetTestClient());
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await app.DisposeAsync();
    }
}
