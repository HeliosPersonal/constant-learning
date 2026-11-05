using ConstantLearning.Features.Content.Availability;
using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using NodaTime.Text;

namespace ConstantLearning.Features.Identity.Languages;

/// <summary>
/// Читач поверхні керування мовами: каталог для вибору (<c>listTargetLanguages</c>) і мови learner-а
/// з активною (<c>listLearnerLanguages</c>). Прапорець <c>available</c> композиціюється тут (T11) з двох
/// джерел T06 — увімкненості мови й наявності схваленого контенту — через <see cref="LanguageAvailability"/>.
/// </summary>
public interface ILanguageDirectory
{
    /// <summary>Повний каталог мов у порядку <c>sort_order</c> з обчисленим <c>available</c>.</summary>
    Task<TargetLanguageCatalogResponse> ListCatalogAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Мови learner-а й активна мова; <c>null</c>, якщо learner ще не завершив онбординг
    /// (→ 404 <c>learner.not_onboarded</c>). Дані лише цього learner (AC-10).
    /// </summary>
    Task<LearnerLanguagesResponse?> ListLearnerLanguagesAsync(Guid learnerId, CancellationToken cancellationToken = default);
}

internal sealed class LanguageDirectory(
    AppDbContext db,
    ITargetLanguageCatalog catalog,
    IApprovedContentLanguages approvedContent) : ILanguageDirectory
{
    public async Task<TargetLanguageCatalogResponse> ListCatalogAsync(CancellationToken cancellationToken = default)
    {
        var languages = await catalog.ListAsync(cancellationToken);
        var approved = await approvedContent.ReadAsync(cancellationToken);
        var items = languages
            .Select(language => new TargetLanguageView(language.Code, language.DisplayNameUk, language.IsEnabled,
                language.SortOrder, LanguageAvailability.IsAvailable(language.IsEnabled, approved.Contains(language.Code))))
            .ToArray();
        return new TargetLanguageCatalogResponse(items);
    }

    public async Task<LearnerLanguagesResponse?> ListLearnerLanguagesAsync(Guid learnerId,
        CancellationToken cancellationToken = default)
    {
        var learner = await db.Learners.AsNoTracking()
            .Where(value => value.Id == learnerId)
            .Select(value => new { value.OnboardedAt, value.ActiveTargetLanguage })
            .SingleOrDefaultAsync(cancellationToken);
        if (learner is null || learner.OnboardedAt is null)
        {
            return null;
        }

        // Назва мови береться з каталогу (join за кодом), порядок — sort_order; без join до dictionary_entries.
        var states = await db.LearnerLanguageStates.AsNoTracking()
            .Where(state => state.LearnerId == learnerId)
            .Join(db.TargetLanguages, state => state.TargetLanguage, language => language.Code,
                (state, language) => new { state.TargetLanguage, language.DisplayNameUk, language.SortOrder, state.CreatedAt })
            .OrderBy(row => row.SortOrder)
            .ToListAsync(cancellationToken);

        var items = states
            .Select(row => new LearnerLanguageView(row.TargetLanguage, row.DisplayNameUk,
                row.TargetLanguage == learner.ActiveTargetLanguage, InstantPattern.ExtendedIso.Format(row.CreatedAt)))
            .ToArray();
        return new LearnerLanguagesResponse(learner.ActiveTargetLanguage, items);
    }
}
