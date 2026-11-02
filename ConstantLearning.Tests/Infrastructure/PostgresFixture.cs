using ConstantLearning.Platform;
using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using NodaTime.Testing;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ConstantLearning.Tests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "PostgreSQL 16";
}

public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    public string ConnectionString => _container?.GetConnectionString()
                                      ?? throw new InvalidOperationException("Тестовий PostgreSQL ще не запущено.");

    public async Task InitializeAsync()
    {
        DockerAvailability.RequireDocker(DockerAvailability.IsCi, DockerAvailability.UnavailableReason);
        if (DockerAvailability.UnavailableReason is not null)
        {
            return;
        }

        _container = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("constant_learning_tests")
            .WithUsername("test_user")
            .WithPassword(Guid.NewGuid().ToString("N"))
            .Build();

        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await _container.StartAsync(timeout.Token);
        await using var provider = CreateServices(new FakeClock(Instant.FromUtc(2026, 10, 31, 9, 0)));
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.MigrateAsync(timeout.Token);
    }

    public ServiceProvider CreateServices(FakeClock clock)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = ConnectionString
        }).Build();

        return new ServiceCollection()
            .AddSingleton(clock)
            .AddSingleton<IClock>(clock)
            .AddPlatform(configuration)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    public async Task ResetAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var tables = new NpgsqlCommand("""
            SELECT 'TRUNCATE TABLE ' || string_agg(format('%I.%I', schemaname, tablename), ', ')
                   || ' RESTART IDENTITY CASCADE'
            FROM pg_tables
            WHERE schemaname = 'public'
              AND tablename NOT IN ('__EFMigrationsHistory', 'morph_forms', 'reference_entries')
            """, connection);
        if (await tables.ExecuteScalarAsync() is string truncate)
        {
            await using var cleanup = new NpgsqlCommand(truncate, connection);
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
