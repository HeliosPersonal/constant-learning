using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace ConstantLearning.Tests.Infrastructure;

/// <summary>Керує життєвим циклом scheduler Quartz у тестовому хості та зупиняє його після тесту.</summary>
internal sealed class QuartzTestLoggingScope : IAsyncDisposable
{
    private readonly IServiceProvider _services;
    private IScheduler? _scheduler;

    public QuartzTestLoggingScope(IServiceProvider services)
    {
        _services = services;
    }

    public async Task<IScheduler> GetSchedulerAsync() =>
        _scheduler ??= await _services.GetRequiredService<ISchedulerFactory>().GetScheduler();

    public async ValueTask DisposeAsync()
    {
        if (_scheduler is not null) await _scheduler.Shutdown();
    }
}
