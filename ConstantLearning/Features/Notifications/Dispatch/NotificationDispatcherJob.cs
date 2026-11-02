using Quartz;

namespace ConstantLearning.Features.Notifications.Dispatch;

[DisallowConcurrentExecution]
public sealed class NotificationDispatcherJob(NotificationDispatcher dispatcher) : IJob
{
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken) =>
        await dispatcher.TickAsync(cancellationToken);
}
