using ConstantLearning.Data.Entities;
using ConstantLearning.Platform;
using ConstantLearning.Platform.Persistence;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;

namespace ConstantLearning.Tests.Platform;

public class PlatformModuleTests
{
    private const string ConnectionString = "Host=localhost;Database=constantlearning;Username=postgres;Password=postgres";

    [Fact]
    public void AddPlatform_RegistersSystemClockAndPostgresWithNodaTime()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        using var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        provider.GetRequiredService<IClock>().Should().BeSameAs(SystemClock.Instance);
        context.Database.ProviderName.Should().Be("Npgsql.EntityFrameworkCore.PostgreSQL");
        var mappings = context.GetService<IRelationalTypeMappingSource>();
        mappings.FindMapping(typeof(Instant))!.StoreType.Should().Be("timestamp with time zone");
        mappings.FindMapping(typeof(LocalDate))!.StoreType.Should().Be("date");
    }

    [Fact]
    public void RuntimeAndDesignTimeModels_PreserveLegacySchema()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        using var runtimeContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        using var designTimeContext = new AppDbContextFactory().CreateDbContext([]);

        foreach (var context in new[] { runtimeContext, designTimeContext })
        {
            context.Database.HasPendingModelChanges().Should().BeFalse(
                "каркас модулів не повинен змінювати наявну схему групового бота");
            context.Model.FindEntityType(typeof(Word))!.GetTableName().Should().Be("Words");
        }
    }

    [Fact]
    public void AddPlatform_UsesSnakeCaseForNewEntities()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<AppDbContext>>();
        using var context = new NamingProbeDbContext(options);

        var entity = context.Model.FindEntityType("NewEntries")!;
        entity.GetTableName().Should().Be("new_entries");
        entity.FindProperty("LearnerId")!.GetColumnName().Should().Be("learner_id");
    }

    private static ServiceProvider CreateProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = ConnectionString
            })
            .Build();

        return new ServiceCollection().AddPlatform(configuration).BuildServiceProvider();
    }

    private sealed class NamingProbeDbContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity("NewEntries", entity =>
            {
                entity.Property<Guid>("Id");
                entity.Property<Guid>("LearnerId");
                entity.HasKey("Id");
            });
        }
    }
}
