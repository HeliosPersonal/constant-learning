using ConstantLearning.Features.Learning.Domain.LearnerDay;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ConstantLearning.Features.Learning;

public static class LearningModule
{
    public static IServiceCollection AddLearning(this IServiceCollection services)
    {
        services.TryAddSingleton<ILearnerDayService, LearnerDayService>();
        return services;
    }
}
