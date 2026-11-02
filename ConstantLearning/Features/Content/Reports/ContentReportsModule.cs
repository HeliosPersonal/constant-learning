namespace ConstantLearning.Features.Content.Reports;

public static class ContentReportsModule
{
    public static IServiceCollection AddContentReports(this IServiceCollection services)
    {
        services.AddScoped<ContentReportService>();
        services.AddScoped<IContentReportService>(provider => provider.GetRequiredService<ContentReportService>());
        return services;
    }
}
