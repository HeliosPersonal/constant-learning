using ConstantLearning.Platform.Persistence;
using ConstantLearning.Services;
using Microsoft.EntityFrameworkCore;

namespace ConstantLearning.HostedServices;

public class DatabaseInitializationService(
    IServiceProvider serviceProvider,
    ILogger<DatabaseInitializationService> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var importService = scope.ServiceProvider.GetRequiredService<IWordImportService>();

        try
        {
            logger.LogInformation("Running database migrations");
            await context.Database.MigrateAsync(cancellationToken);

            await importService.ImportFromCsvAsync();

            logger.LogInformation("Database initialization completed");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during database initialization");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}