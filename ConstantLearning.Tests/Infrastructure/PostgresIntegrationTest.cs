using ConstantLearning.Platform.Persistence;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using NodaTime.Testing;

namespace ConstantLearning.Tests.Infrastructure;

[Collection(PostgresCollection.Name)]
public abstract class PostgresIntegrationTest(PostgresFixture postgres) : IAsyncLifetime
{
    private ServiceProvider? _provider;
    private AsyncServiceScope _scope;

    protected FakeClock Clock { get; } = new(Instant.FromUtc(2026, 10, 31, 9, 0));
    protected IServiceProvider Services => _scope.ServiceProvider;
    protected AppDbContext Db => Services.GetRequiredService<AppDbContext>();

    public async Task InitializeAsync()
    {
        await postgres.ResetAsync();
        _provider = postgres.CreateServices(Clock);
        _scope = _provider.CreateAsyncScope();
    }

    public async Task DisposeAsync()
    {
        if (_provider is not null)
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }
}
