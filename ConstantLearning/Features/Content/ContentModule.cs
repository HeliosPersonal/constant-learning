using ConstantLearning.Features.Content.Availability;
using ConstantLearning.Features.Content.Course;
using ConstantLearning.Features.Content.Learning;
using ConstantLearning.Features.Content.Editor.Queue;
using ConstantLearning.Features.Content.Editor.Rollback;
using ConstantLearning.Features.Content.Import;
using ConstantLearning.Features.Content.Notifications;
using ConstantLearning.Features.Content.Publishing;
using ConstantLearning.Features.Content.Search.Forms;
using ConstantLearning.Features.Content.References;
using ConstantLearning.Features.Content.Search.Api;
using ConstantLearning.Features.Content.Reports;
using ConstantLearning.Features.Content.Verification;

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
        services.AddPublishedCourseReader();
        services.AddContentAvailability();
        services.AddAnswerContentReader();
        services.AddContentReports();
        services.AddContentVerification();
        services.AddScoped<EditorQueueService>();
        services.AddScoped<AppliedChangesService>();
        return services;
    }
}
