using ConstantLearning.Platform.Persistence;
using ConstantLearning.Features.Content.Import;
using ConstantLearning.Features.Content.Course;
using ConstantLearning.Features.Content.Search.Forms;
using ConstantLearning.Features.Content.References;
using Microsoft.EntityFrameworkCore;

namespace ConstantLearning.HostedServices;

public class DatabaseInitializationService(
    IServiceProvider serviceProvider,
    IHostApplicationLifetime lifetime,
    ILogger<DatabaseInitializationService> logger)
    : IHostedService
{
    private Task? _referenceImport;

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
            await scope.ServiceProvider.GetRequiredService<SearchFormsMaintenance>().InitializeAsync(cancellationToken);
            // Незмінні версії готові до перших одночасних відкриттів тренера.
            await scope.ServiceProvider.GetRequiredService<IPublishedCourseReader>().ReadAsync(cancellationToken);

            logger.LogInformation("Ініціалізацію бази даних завершено");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Помилка ініціалізації бази даних");
            throw;
        }

        // Довідкові знімки великі (≈8 000 рядків, окрема транзакція на рядок), тож їх імпорт не блокує старт:
        // інакше startup probe вбиває pod, поки застосунок ще не почав слухати порт. Імпорт ідемпотентний,
        // а незавершений прохід продовжується після наступного старту.
        _referenceImport = Task.Run(() => ImportReferenceSnapshotsAsync(lifetime.ApplicationStopping));
    }

    private async Task ImportReferenceSnapshotsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var importer = scope.ServiceProvider.GetRequiredService<ReferenceSnapshotImporter>();
            var references = await importer.ImportPackagedAsync(cancellationToken);
            logger.LogInformation("Довідкові знімки: імпортовано {Imported}, без змін {Unchanged}, потребують редактора {ManualRecords}",
                references.Imported, references.Unchanged, references.ManualRecords);
            var configuredReferences = await importer
                .ImportConfiguredDumpAsync(scope.ServiceProvider.GetRequiredService<IConfiguration>(), cancellationToken);
            logger.LogInformation("Локальний дамп: імпортовано {Imported}, без змін {Unchanged}, потребують редактора {ManualRecords}",
                configuredReferences.Imported, configuredReferences.Unchanged, configuredReferences.ManualRecords);
            foreach (var issue in references.Issues.Concat(configuredReferences.Issues))
                logger.LogWarning("Довідковий запис потребує редактора: {Lemma}, причина {Reason}", issue.Lemma, issue.Reason);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Імпорт довідкових знімків перервано зупинкою застосунку");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Помилка імпорту довідкових знімків");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_referenceImport is null) return;
        try
        {
            await _referenceImport.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Зупинка має тривати далі; фонову задачу вже скасовано через ApplicationStopping.
        }
    }
}
