using Quartz;

namespace ConstantLearning.Features.Notifications.Summaries;

[DisallowConcurrentExecution]
public sealed class SummaryPlannerJob(SummaryPlanner planner) : IJob
{
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken) => await planner.TickAsync(cancellationToken);
}
