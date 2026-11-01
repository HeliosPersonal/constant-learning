using ConstantLearning.Platform.Persistence;
using ConstantLearning.Features.Content.Import;
using ConstantLearning.Features.Content.Search.Forms;
using ConstantLearning.Features.Content.References;
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
            var references = await scope.ServiceProvider.GetRequiredService<ReferenceSnapshotImporter>().ImportPackagedAsync(cancellationToken);
            logger.LogInformation("Довідкові знімки: імпортовано {Imported}, без змін {Unchanged}, потребують редактора {ManualRecords}",
                references.Imported, references.Unchanged, references.ManualRecords);
            var configuredReferences = await scope.ServiceProvider.GetRequiredService<ReferenceSnapshotImporter>()
                .ImportConfiguredDumpAsync(scope.ServiceProvider.GetRequiredService<IConfiguration>(), cancellationToken);
            logger.LogInformation("Локальний дамп: імпортовано {Imported}, без змін {Unchanged}, потребують редактора {ManualRecords}",
                configuredReferences.Imported, configuredReferences.Unchanged, configuredReferences.ManualRecords);
            foreach (var issue in references.Issues.Concat(configuredReferences.Issues))
                logger.LogWarning("Довідковий запис потребує редактора: {Lemma}, причина {Reason}", issue.Lemma, issue.Reason);
            await scope.ServiceProvider.GetRequiredService<SearchFormsMaintenance>().InitializeAsync(cancellationToken);

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
