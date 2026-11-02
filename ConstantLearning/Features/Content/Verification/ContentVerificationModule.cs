using Quartz;

namespace ConstantLearning.Features.Content.Verification;

public static class ContentVerificationModule
{
    public static IServiceCollection AddContentVerification(this IServiceCollection services)
    {
        services.AddScoped<ContentVerificationWorker>();
        services.AddQuartz(options =>
        {
            var key = new JobKey("ContentVerification");
            options.AddJob<ContentVerificationJob>(job => job.WithIdentity(key));
            options.AddTrigger(trigger => trigger.ForJob(key).WithIdentity("ContentVerification-trigger")
                .WithSimpleSchedule(schedule => schedule.WithIntervalInSeconds(30).RepeatForever()));
        });
        return services;
    }
}

[DisallowConcurrentExecution]
public sealed class ContentVerificationJob(ContentVerificationWorker worker) : IJob
{
    public async Task Execute(IJobExecutionContext context) => await worker.TickAsync(context.CancellationToken);
}
