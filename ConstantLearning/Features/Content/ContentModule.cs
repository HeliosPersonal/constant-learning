using ConstantLearning.Features.Content.Import;
using ConstantLearning.Features.Content.Notifications;
using ConstantLearning.Features.Content.Publishing;
using ConstantLearning.Features.Content.Search.Forms;
using ConstantLearning.Features.Content.References;
using ConstantLearning.Features.Content.Search.Api;
using ConstantLearning.Features.Content.Reports;

namespace ConstantLearning.Features.Content;

public static class ContentModule
{
    public static IServiceCollection AddContent(this IServiceCollection services)
    {
        services.AddSingleton<DictionaryImportReader>();
        services.AddScoped<DictionaryImportService>();
        services.AddScoped<ContentPublicationService>();
        services.AddScoped<SearchFormsProjection>();
        services.AddScoped<IPublicationProjection>(provider => provider.GetRequiredService<SearchFormsProjection>());
        services.AddScoped<PolishMorphologyImporter>();
        services.AddScoped<SearchFormsMaintenance>();
        services.AddReportSummaryDispatch();
        services.AddReferenceSources();
        services.AddDictionarySearch();
        services.AddContentReports();
        return services;
    }
}
