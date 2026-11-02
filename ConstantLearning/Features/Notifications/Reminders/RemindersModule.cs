using ConstantLearning.Features.Identity.Settings;
using ConstantLearning.Features.Notifications.Dispatch;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quartz;

namespace ConstantLearning.Features.Notifications.Reminders;

public static class RemindersModule
{
    public static IServiceCollection AddReminders(this IServiceCollection services)
    {
        services.TryAddScoped<ReminderPlanner>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<INotificationHandler, ReminderHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ILearnerSettingsChangedHandler, ReminderSettingsChangedHandler>());
        services.AddQuartz(options =>
        {
            var key = new JobKey("ReminderPlanner");
            options.AddJob<ReminderPlannerJob>(job => job.WithIdentity(key));
            options.AddTrigger(trigger => trigger.ForJob(key).WithIdentity("ReminderPlanner-trigger")
                .WithSimpleSchedule(schedule => schedule.WithInterval(TimeSpan.FromSeconds(30)).RepeatForever()));
        });
        return services;
    }
}

[DisallowConcurrentExecution]
public sealed class ReminderPlannerJob(ReminderPlanner planner) : IJob
{
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken) => await planner.TickAsync(cancellationToken);
}
