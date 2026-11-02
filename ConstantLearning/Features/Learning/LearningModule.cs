using ConstantLearning.Features.Learning.Today;
using ConstantLearning.Features.Learning.Domain.LearnerDay;
using ConstantLearning.Features.Learning.Words;
using ConstantLearning.Features.Learning.Sessions.Start;
using ConstantLearning.Features.Learning.Sessions.Start.Limits;
using ConstantLearning.Features.Learning.Sessions.Current;
using ConstantLearning.Features.Learning.Sessions.DayBoundary;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ConstantLearning.Features.Learning;

public static class LearningModule
{
    public static IServiceCollection AddLearning(this IServiceCollection services)
    {
        services.TryAddSingleton<ILearnerDayService, LearnerDayService>();
        services.TryAddScoped<ILearnerWordStatusReader, LearnerWordStatusReader>();
        services.AddAnswerDisputeSourceReader();
        services.TryAddScoped<StartSessionService>();
        services.TryAddScoped<SessionLimitsService>();
        services.TryAddScoped<SessionDayBoundaryService>();
        services.TryAddScoped<CurrentSessionService>();
        services.AddToday();
        return services;
    }
}
