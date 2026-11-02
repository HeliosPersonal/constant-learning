using Microsoft.EntityFrameworkCore;

namespace ConstantLearning.Platform.Persistence;

internal static class PersistenceOptions
{
    public static void Configure(DbContextOptionsBuilder options, string connectionString)
    {
        options.UseNpgsql(connectionString, npgsql => npgsql.UseNodaTime())
            .UseSnakeCaseNamingConvention();
    }
}
