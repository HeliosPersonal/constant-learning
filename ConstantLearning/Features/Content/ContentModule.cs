using ConstantLearning.Features.Content.Import;
using ConstantLearning.Features.Content.Notifications;
using ConstantLearning.Features.Content.Publishing;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ConstantLearning.Features.Content;

public static class ContentModule
{
    public static IServiceCollection AddContent(this IServiceCollection services)
    {
        services.AddSingleton<DictionaryImportReader>();
        services.AddScoped<DictionaryImportService>();
        services.AddScoped<ContentPublicationService>();
        services.AddReportSummaryDispatch();
        services.TryAddScoped<IPublicationProjection, NoOpPublicationProjection>();
        return services;
    }
}
