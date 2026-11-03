using ConstantLearning.Features.Learning.Sessions.NewWords;
using ConstantLearning.Features.Learning.Sessions.Results;
using ConstantLearning.Features.Learning.Disputes;
using ConstantLearning.Features.Learning.Domain.LearnerDay;
using ConstantLearning.Features.Learning.Words;
using ConstantLearning.Features.Learning.Sessions.Start;
using ConstantLearning.Features.Learning.Sessions.Start.Limits;
using ConstantLearning.Features.Learning.Sessions.Current;
using ConstantLearning.Features.Learning.Sessions.DayBoundary;
using ConstantLearning.Features.Learning.Sessions.Extra;
using ConstantLearning.Features.Learning.Sessions.Answer;
using ConstantLearning.Features.Learning.Domain.Scheduling;
using ConstantLearning.Features.Learning.Stats;
using ConstantLearning.Features.Learning.Today;
using ConstantLearning.Features.Learning.Notifications;
using ConstantLearning.Features.Learning.Reset;
using ConstantLearning.Features.Identity.ProgressReset;
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
        services.TryAddScoped<StartExtraWordService>();
        services.TryAddScoped<StartExtraPracticeService>();
        services.TryAddScoped<SessionLimitsService>();
        services.TryAddScoped<SessionDayBoundaryService>();
        services.TryAddScoped<CurrentSessionService>();
        services.TryAddSingleton<IReviewScheduler, MemorizeReviewScheduler>();
        services.TryAddScoped<AnswerSessionService>();
        services.TryAddScoped<DisputeService>();
        services.TryAddScoped<ContinueSessionService>();
        services.TryAddScoped<LearnedWordsStatsService>();
        services.AddToday();
        services.AddLearningNotifications();
        services.AddNewWords();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IProgressResetHandler, LearningProgressResetHandler>());
        return services;
    }
}
