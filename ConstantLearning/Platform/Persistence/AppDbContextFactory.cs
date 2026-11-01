using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ConstantLearning.Platform.Persistence;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        PersistenceOptions.Configure(optionsBuilder,
            "Host=localhost;Database=constantlearning;Username=postgres;Password=postgres");

        return new AppDbContext(optionsBuilder.Options);
    }
}
