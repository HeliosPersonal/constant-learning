using ConstantLearning.Platform.Metrics;
using ConstantLearning.Platform.Persistence;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NodaTime;

namespace ConstantLearning.Platform;

public static class PlatformModule
{
    public static IServiceCollection AddPlatform(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Constant_Learning")
                               ?? configuration.GetConnectionString("DefaultConnection")
                               ?? throw new InvalidOperationException(
                                   "Не знайдено рядок підключення 'Constant_Learning' або локальний 'DefaultConnection'.");

        services.TryAddSingleton<IClock>(SystemClock.Instance);
        services.TryAddSingleton<TrainerMetrics>();
        services.AddDbContext<AppDbContext>(options => PersistenceOptions.Configure(options, connectionString));

        return services;
    }
}
