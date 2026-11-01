using Microsoft.Extensions.DependencyInjection.Extensions;
using NodaTime;

namespace ConstantLearning.Features.Content.References;

public static class ReferenceSourcesModule
{
    public static IServiceCollection AddReferenceSources(this IServiceCollection services, Action<ReferenceSourcesOptions>? configure = null)
    {
        var options = new ReferenceSourcesOptions();
        configure?.Invoke(options);
        if (options.WsjpMinimumInterval < TimeSpan.FromSeconds(1) || options.WsjpTimeout <= TimeSpan.Zero ||
            options.WsjpTimeout > TimeSpan.FromSeconds(30) || options.WsjpCacheLifetime < TimeSpan.FromMinutes(1) ||
            options.WsjpMaxResponseBytes is < 1 or > 1_048_576)
            throw new ArgumentException("Некоректні обмеження довідкового джерела.", nameof(configure));
        services.TryAddSingleton<IClock>(SystemClock.Instance);
        services.AddSingleton(options);
        services.AddSingleton<PolishIpaConverter>();
        services.AddSingleton<WikislownikParser>();
        services.AddScoped<ReferenceSnapshotImporter>();
        services.AddScoped<WikislownikReferenceSource>();
        services.AddScoped<IReferenceSource>(provider => provider.GetRequiredService<WikislownikReferenceSource>());
        services.AddSingleton<GovernmentReferenceSource>();
        services.AddSingleton<IReferenceSource>(provider => provider.GetRequiredService<GovernmentReferenceSource>());
        services.AddHttpClient("reference-wsjp").ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = false, UseCookies = false, AutomaticDecompression = System.Net.DecompressionMethods.None
        });
        services.AddSingleton<WsjpReferenceSource>(provider => new WsjpReferenceSource(
            provider.GetRequiredService<IHttpClientFactory>().CreateClient("reference-wsjp"), options));
        services.AddSingleton<IReferenceSource>(provider => provider.GetRequiredService<WsjpReferenceSource>());
        return services;
    }
}
