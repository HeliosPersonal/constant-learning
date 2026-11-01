using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConstantLearning.Features.Content.Notifications;

public interface IReportSummaryDispatchService
{
    // Викликається в транзакції зміни статусу відправлення зі спільним scoped AppDbContext.
    Task ReleaseAsync(Guid dispatchId, CancellationToken cancellationToken);
}

internal sealed class ReportSummaryDispatchService(AppDbContext db) : IReportSummaryDispatchService
{
    public async Task ReleaseAsync(Guid dispatchId, CancellationToken cancellationToken) =>
        await db.ContentReports.Where(report => report.SummaryDispatchId == dispatchId)
            .ExecuteUpdateAsync(update => update.SetProperty(report => report.SummaryDispatchId, (Guid?)null),
                cancellationToken);
}

public static class ReportSummaryDispatchModule
{
    public static IServiceCollection AddReportSummaryDispatch(this IServiceCollection services)
    {
        services.AddScoped<IReportSummaryDispatchService, ReportSummaryDispatchService>();
        return services;
    }
}
