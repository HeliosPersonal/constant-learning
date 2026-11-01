using ConstantLearning.Platform.Persistence;
using ConstantLearning.Features.Content.Import;
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
        var importService = scope.ServiceProvider.GetRequiredService<DictionaryImportService>();

        try
        {
            logger.LogInformation("Виконуємо міграції бази даних");
            await context.Database.MigrateAsync(cancellationToken);

            await importService.ImportPackagedAsync(cancellationToken);

            logger.LogInformation("Ініціалізацію бази даних завершено");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Помилка ініціалізації бази даних");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
