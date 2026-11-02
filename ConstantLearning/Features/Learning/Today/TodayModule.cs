using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ConstantLearning.Features.Learning.Today;

public static class TodayModule
{
    public static IServiceCollection AddToday(this IServiceCollection services)
    {
        services.TryAddScoped<TodayService>();
        return services;
    }
}
