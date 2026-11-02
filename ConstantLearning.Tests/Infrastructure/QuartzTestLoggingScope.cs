using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Quartz;
using Quartz.Logging;

namespace ConstantLearning.Tests.Infrastructure;

/// <summary>Ізолює статичне журналювання Quartz між послідовними тестовими хостами.</summary>
internal sealed class QuartzTestLoggingScope : IAsyncDisposable
{
    private readonly IServiceProvider _services;
    private IScheduler? _scheduler;

    public QuartzTestLoggingScope(IServiceProvider services)
    {
        _services = services;
        // ContainerConfigurationProcessor читає static provider ще до ініціалізації scheduler factory.
        LogContext.SetCurrentLogProvider(services.GetRequiredService<ILoggerFactory>());
    }

    public async Task<IScheduler> GetSchedulerAsync() =>
        _scheduler ??= await _services.GetRequiredService<ISchedulerFactory>().GetScheduler();

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_scheduler is not null) await _scheduler.Shutdown();
        }
        finally
        {
            // Singleton не залежить від lifetime хоста; скидаємо до Dispose його LoggerFactory.
            LogContext.SetCurrentLogProvider(NullLoggerFactory.Instance);
        }
    }
}
