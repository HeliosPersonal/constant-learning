using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace ConstantLearning.Platform.Persistence;

internal static class PersistenceOptions
{
    public static void Configure(DbContextOptionsBuilder options, string connectionString)
    {
        // У образі немає libgssapi_krb5, а Postgres у кластері не використовує GSS: вимикаємо спробу GSS-шифрування.
        var builder = new NpgsqlConnectionStringBuilder(connectionString) { GssEncryptionMode = GssEncryptionMode.Disable };
        options.UseNpgsql(builder.ConnectionString, npgsql => npgsql.UseNodaTime())
            .UseSnakeCaseNamingConvention()
            // CREATE INDEX CONCURRENTLY навмисно виконується поза транзакцією (suppressTransaction), як вимагають правила міграцій.
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.MigrationsUserTransactionWarning));
    }
}
