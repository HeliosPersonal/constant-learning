using Quartz;

namespace ConstantLearning.Features.Notifications.Summaries;

[DisallowConcurrentExecution]
public sealed class SummaryPlannerJob(SummaryPlanner planner) : IJob
{
    public async Task Execute(IJobExecutionContext context) => await planner.TickAsync(context.CancellationToken);
}
