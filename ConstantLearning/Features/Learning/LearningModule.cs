using ConstantLearning.Features.Learning.Domain.LearnerDay;
using ConstantLearning.Features.Learning.Words;
using ConstantLearning.Features.Learning.Sessions.Start;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ConstantLearning.Features.Learning;

public static class LearningModule
{
    public static IServiceCollection AddLearning(this IServiceCollection services)
    {
        services.TryAddSingleton<ILearnerDayService, LearnerDayService>();
        services.TryAddScoped<ILearnerWordStatusReader, LearnerWordStatusReader>();
        services.TryAddScoped<StartSessionService>();
        return services;
    }
}
