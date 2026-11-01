using System.Security.Cryptography;
using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ConstantLearning.Features.Identity.Aliases;

internal sealed class RandomReporterAliasGenerator : IReporterAliasGenerator
{
    public string Create() => "R-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(6));
}

internal sealed class ReporterAliasService(AppDbContext db, IReporterAliasGenerator generator) : IReporterAliasService
{
    public async Task<string> GetOrCreateAsync(Guid learnerId, CancellationToken cancellationToken = default)
    {
        var transaction = db.Database.CurrentTransaction
                          ?? throw new InvalidOperationException("Створення псевдоніма потребує транзакції викликувача.");
        var learner = await db.Learners.FromSqlInterpolated($"""
            SELECT *, xmin FROM learners WHERE id = {learnerId} AND onboarded_at IS NOT NULL FOR UPDATE
            """).AsNoTracking().SingleOrDefaultAsync(cancellationToken)
                      ?? throw new InvalidOperationException("Псевдонім потребує завершеного профілю learner.");
        if (learner.ReporterAlias is { } existing)
        {
            return existing;
        }

        var tracked = db.Learners.Local.SingleOrDefault(value => value.Id == learnerId);
        if (tracked is not null && db.Entry(tracked).State != EntityState.Unchanged)
        {
            throw new InvalidOperationException("Створіть псевдонім до змін learner.");
        }

        for (var attempt = 0; attempt < 8; attempt++)
        {
            var alias = generator.Create();
            await transaction.CreateSavepointAsync("reporter_alias_candidate", cancellationToken);
            try
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE learners SET reporter_alias = {alias} WHERE id = {learnerId} AND reporter_alias IS NULL
                    """, cancellationToken);
                await transaction.ReleaseSavepointAsync("reporter_alias_candidate", cancellationToken);
                if (tracked is not null)
                {
                    await db.Entry(tracked).ReloadAsync(cancellationToken);
                }

                return alias;
            }
            catch (PostgresException exception) when (exception is
                   { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ux_learners_reporter_alias" })
            {
                // PostgreSQL після unique violation приймає нові команди лише після rollback до savepoint.
                await transaction.RollbackToSavepointAsync("reporter_alias_candidate", cancellationToken);
                await transaction.ReleaseSavepointAsync("reporter_alias_candidate", cancellationToken);
            }
        }

        throw new InvalidOperationException("Не вдалося створити унікальний псевдонім автора.");
    }
}
