using ConstantLearning.Features.Notifications.Dispatch;
using ConstantLearning.Features.Notifications.Summaries;
using ConstantLearning.Features.Notifications.Reminders;
using ConstantLearning.Features.Identity.Settings;
using Quartz;

namespace ConstantLearning.Features.Notifications;

public static class NotificationsModule
{
    public static IServiceCollection AddNotifications(this IServiceCollection services)
    {
        services.AddScoped<NotificationDispatcher>();
        services.AddScoped<SummaryPlanner>();
        services.AddScoped<INotificationHandler, ReportOutcomesHandler>();
        services.AddScoped<INotificationHandler, EditorDigestHandler>();
        services.AddScoped<ILearnerSettingsChangedHandler, SummarySettingsChangedHandler>();
        services.AddReminders();
        services.AddQuartz(options =>
        {
            var key = new JobKey("SummaryPlanner");
            options.AddJob<SummaryPlannerJob>(job => job.WithIdentity(key));
            options.AddTrigger(trigger => trigger.ForJob(key).WithIdentity("SummaryPlanner-trigger")
                .WithSimpleSchedule(schedule => schedule.WithInterval(TimeSpan.FromSeconds(30)).RepeatForever()));
        });
        return services;
    }
}
