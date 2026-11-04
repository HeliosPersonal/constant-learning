using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConstantLearning.Features.Identity.Languages;

internal sealed class TargetLanguageCatalog(AppDbContext db) : ITargetLanguageCatalog
{
    public async Task<IReadOnlyList<TargetLanguage>> ListAsync(CancellationToken cancellationToken = default) =>
        await db.TargetLanguages.AsNoTracking()
            .OrderBy(language => language.SortOrder)
            .ToListAsync(cancellationToken);

    public Task<TargetLanguage?> FindAsync(string code, CancellationToken cancellationToken = default) =>
        db.TargetLanguages.AsNoTracking()
            .SingleOrDefaultAsync(language => language.Code == code, cancellationToken);
}
