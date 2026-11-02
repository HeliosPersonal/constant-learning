using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ConstantLearning.Features.Learning.Sessions.NewWords;

public static class NewWordsModule
{
    public static IServiceCollection AddNewWords(this IServiceCollection services)
    {
        services.TryAddScoped<NewWordService>();
        return services;
    }
}
