namespace ConstantLearning.Features.Bot;

public static class BotModule
{
    public static IServiceCollection AddBot(this IServiceCollection services)
    {
        services.AddScoped<BotUpdateHandler>();
        return services;
    }
}
