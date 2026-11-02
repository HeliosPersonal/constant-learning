using Quartz;

namespace ConstantLearning.Features.Notifications.Dispatch;

[DisallowConcurrentExecution]
public sealed class NotificationDispatcherJob(NotificationDispatcher dispatcher) : IJob
{
    public async Task Execute(IJobExecutionContext context) =>
        await dispatcher.TickAsync(context.CancellationToken);
}
