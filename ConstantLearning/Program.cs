using ConstantLearning.Configuration;
using ConstantLearning.Features.Bot;
using ConstantLearning.Features.Content;
using ConstantLearning.Features.Identity;
using ConstantLearning.Features.Learning;
using ConstantLearning.Features.Notifications;
using ConstantLearning.Features.Notifications.Dispatch;
using ConstantLearning.HostedServices;
using ConstantLearning.Platform;
using ConstantLearning.Platform.Metrics;
using ConstantLearning.Platform.Telegram;
using Quartz;

namespace ConstantLearning;

public static class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.AddEnvVariablesAndConfigureSecrets();

        builder.Services.AddPlatform(builder.Configuration);
        builder.Services.AddIdentity(builder.Configuration);
        builder.Services.AddLearning();
        builder.Services.AddContent();
        builder.Services.AddNotifications();
        builder.Services.AddBot();
        builder.Services.AddTelegram();

        builder.Services.AddQuartz(q =>
        {
            var dispatcherJobKey = new JobKey("NotificationDispatcher");
            q.AddJob<NotificationDispatcherJob>(opts => opts.WithIdentity(dispatcherJobKey));
            q.AddTrigger(opts => opts.ForJob(dispatcherJobKey)
                .WithIdentity("NotificationDispatcher-trigger")
                .WithSimpleSchedule(schedule => schedule.WithInterval(TimeSpan.FromSeconds(30)).RepeatForever()));

        });

        builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

        builder.Services.AddHostedService<DatabaseInitializationService>();
        builder.Services.AddHostedService<WebhookConfigurationService>();

        builder.Services.AddControllers();
        builder.Services.AddOpenApi();

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.UseRouting();
        app.UseMiddleware<ServerTimingMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
        app.MapLocalMetrics();

        // Невідомі API-маршрути не повинні повертати HTML оболонки.
        app.MapFallback("/api/{**path}", () => Results.NotFound());
        app.MapFallbackToFile("index.html");

        app.Run();
    }
}
