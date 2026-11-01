using ConstantLearning.Features.Notifications.Dispatch;

namespace ConstantLearning.Features.Notifications;

public static class NotificationsModule
{
    public static IServiceCollection AddNotifications(this IServiceCollection services)
    {
        services.AddScoped<NotificationDispatcher>();
        return services;
    }
}
