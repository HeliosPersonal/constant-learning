using ConstantLearning.Features.Content.Data;
using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace ConstantLearning.Features.Content.Publishing;

public sealed record PublicationChange(Guid EntryId, Guid? ExpectedPublishedVersionId, Guid? ToVersionId,
    string ActorKind, Guid? ActorLearnerId = null, string Action = "publish");

/// <summary>Оновлює похідні дані в поточній транзакції та тому самому scoped AppDbContext.</summary>
public interface IPublicationProjection
{
    Task UpdateAsync(IReadOnlyList<PublicationChange> changes, CancellationToken cancellationToken);
}

/// <summary>До реалізації пошукової проєкції публікація не залежить від її таблиць.</summary>
public sealed class NoOpPublicationProjection : IPublicationProjection
{
    public Task UpdateAsync(IReadOnlyList<PublicationChange> changes, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

public sealed class ContentPublicationService(AppDbContext db, IPublicationProjection projection, IClock clock)
{
    /// <summary>Викликати перед створенням версій; блокування діє до завершення транзакції.</summary>
    public Task AcquireLockAsync(CancellationToken cancellationToken = default)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Публікація потребує активної транзакції.");
        return db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(6147202601)", cancellationToken);
    }

    public async Task<int> ApplyAsync(IReadOnlyList<PublicationChange> changes,
        CancellationToken cancellationToken = default)
    {
        var ownsTransaction = db.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        try
        {
            await AcquireLockAsync(cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            if (changes.Select(change => change.EntryId).Distinct().Count() != changes.Count)
                throw new ArgumentException("Пакет має містити одну дію для кожного запису.", nameof(changes));
            var ids = changes.Select(change => change.EntryId).ToArray();
            var entries = await db.DictionaryEntries.Where(entry => ids.Contains(entry.Id))
                .ToDictionaryAsync(entry => entry.Id, cancellationToken);
            var versionIds = changes.Where(change => change.ToVersionId.HasValue)
                .Select(change => change.ToVersionId!.Value).ToArray();
            // Блокування версій не дозволяє паралельно зняти approved між перевіркою та commit.
            var versions = await db.EntryVersions.FromSqlInterpolated(
                    $"SELECT *, xmin FROM entry_versions WHERE id = ANY({versionIds}) FOR UPDATE")
                .AsNoTracking()
                .ToDictionaryAsync(version => version.Id, cancellationToken);
            var applied = new List<PublicationChange>();
            foreach (var change in changes)
            {
                if (change.ActorKind is not ("import" or "editor" or "auto_verification")
                    || change.Action is not ("publish" or "unpublish" or "rollback")
                    || change.Action == "publish" && change.ToVersionId is null
                    || change.Action == "unpublish" && change.ToVersionId is not null)
                    throw new ArgumentException("Невідповідна дія публікації.", nameof(changes));
                if (!entries.TryGetValue(change.EntryId, out var entry))
                    throw new InvalidOperationException("Запис словника не існує.");
                if (entry.PublishedVersionId != change.ExpectedPublishedVersionId)
                    throw new DbUpdateConcurrencyException("Опублікована версія вже змінилася.");
                if (change.ToVersionId is { } versionId
                    && (!versions.TryGetValue(versionId, out var version) || version.EntryId != entry.Id
                        || version.ValidationStatus != "approved"))
                    throw new InvalidOperationException("Публікувати можна лише approved-версію цього запису.");
                if (entry.PublishedVersionId == change.ToVersionId) continue;
                entry.PublishedVersionId = change.ToVersionId;
                if (change.ToVersionId is null) entry.CoursePosition = null;
                db.PublicationLog.Add(new PublicationLog
                {
                    EntryId = entry.Id, FromVersionId = change.ExpectedPublishedVersionId,
                    ToVersionId = change.ToVersionId, Action = change.Action,
                    ActorKind = change.ActorKind, ActorLearnerId = change.ActorLearnerId,
                    CreatedAt = clock.GetCurrentInstant()
                });
                applied.Add(change);
            }
            await db.SaveChangesAsync(cancellationToken);
            if (applied.Count > 0)
            {
                await ReorderCourseAsync(cancellationToken);
                await projection.UpdateAsync(applied.AsReadOnly(), cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
            }
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return applied.Count;
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                db.ChangeTracker.Clear();
            }
            throw;
        }
    }

    private async Task ReorderCourseAsync(CancellationToken cancellationToken)
    {
        var entries = await db.DictionaryEntries.Include(entry => entry.PublishedVersion)
            .Where(entry => entry.PublishedVersionId != null && entry.PublishedVersion!.ValidationStatus == "approved")
            .ToListAsync(cancellationToken);
        var everyday = new Queue<DictionaryEntry>(entries.Where(entry => entry.Domain == "everyday")
            .OrderBy(entry => entry.PublishedVersion!.FrequencyRank ?? int.MaxValue)
            .ThenBy(entry => entry.ExternalId, StringComparer.Ordinal));
        var legalization = new Queue<DictionaryEntry>(entries.Where(entry => entry.Domain == "legalization")
            .OrderBy(entry => entry.PublishedVersion!.UtilityRank ?? int.MaxValue)
            .ThenBy(entry => entry.ExternalId, StringComparer.Ordinal));
        var everydayTotal = everyday.Count;
        var legalizationTotal = legalization.Count;
        var position = 1;
        while (everyday.Count + legalization.Count > 0)
        {
            var takeEveryday = everyday.Count > 0 && (legalization.Count == 0
                || (long)(everydayTotal - everyday.Count) * legalizationTotal
                <= (long)(legalizationTotal - legalization.Count) * everydayTotal);
            (takeEveryday ? everyday : legalization).Dequeue().CoursePosition = position++;
        }
    }
}
