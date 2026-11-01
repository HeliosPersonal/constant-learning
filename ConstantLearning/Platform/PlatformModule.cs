using ConstantLearning.Platform.Persistence;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NodaTime;

namespace ConstantLearning.Platform;

public static class PlatformModule
{
    public static IServiceCollection AddPlatform(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
                               ?? throw new InvalidOperationException(
                                   "Не знайдено рядок підключення 'DefaultConnection'.");

        services.TryAddSingleton<IClock>(SystemClock.Instance);
        services.AddDbContext<AppDbContext>(options => PersistenceOptions.Configure(options, connectionString));

        return services;
    }
}
