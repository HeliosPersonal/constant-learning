namespace ConstantLearning.Features.Content.Search.Api;

public static class DictionarySearchModule
{
    public static IServiceCollection AddDictionarySearch(this IServiceCollection services)
    {
        services.AddScoped<DictionarySearchService>();
        return services;
    }
}
