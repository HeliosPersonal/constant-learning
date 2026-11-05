using ConstantLearning.Features.Learning.Words;
using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConstantLearning.Features.Content.Search.Api;

public sealed class DictionarySearchService(AppDbContext db, ILearnerWordStatusReader statuses)
{
    public async Task<DictionarySearchPage?> SearchAsync(Guid learnerId, DictionarySearchRequest request,
        CancellationToken cancellationToken)
    {
        // Словник читається у вимірі активної мови learner-а (AC-03): показуємо лише її записи,
        // решта мов недосяжні з цього запиту. Інтеримний 'pl', доки активна мова не почата (як у LearnerDayStateService).
        var language = await ResolveActiveLanguageAsync(learnerId, cancellationToken);
        var forms = db.SearchForms.AsNoTracking().Where(form => form.FormFolded == request.FoldedQuery
            || (request.Prefix && form.FormFolded.StartsWith(request.FoldedQuery)));
        var candidates = from entry in db.DictionaryEntries.AsNoTracking()
            join version in db.EntryVersions.AsNoTracking() on entry.PublishedVersionId equals version.Id
            where entry.TargetLanguage == language && version.EntryId == entry.Id && version.ValidationStatus == "approved"
                  && forms.Any(form => form.EntryId == entry.Id && form.VersionId == version.Id)
            select new SearchRow
            {
                Id = entry.Id,
                CoursePosition = entry.CoursePosition ?? int.MaxValue,
                Exact = forms.Any(form => form.EntryId == entry.Id && form.VersionId == version.Id
                                          && form.FormFolded == request.FoldedQuery),
                Lemma = version.LemmaPl,
                PartOfSpeech = version.PartOfSpeech,
                Meaning = db.VersionTranslations.Where(translation => translation.VersionId == version.Id)
                    .OrderBy(translation => translation.Kind == "canonical" ? 0 : 1)
                    .ThenBy(translation => translation.Position).ThenBy(translation => translation.Id)
                    .Select(translation => translation.TextUk).FirstOrDefault() ?? "",
                MatchedForm = forms.Where(form => form.EntryId == entry.Id && form.VersionId == version.Id)
                    .OrderByDescending(form => form.FormFolded == request.FoldedQuery)
                    .ThenByDescending(form => form.IsLemma).ThenBy(form => form.FormDisplay)
                    .Select(form => form.FormDisplay).First()
            };

        var window = candidates;
        if ((request.After ?? request.Before) is { } cursor)
        {
            var anchor = await candidates.FirstOrDefaultAsync(row => row.Id == cursor, cancellationToken);
            if (anchor is null) return null;
            window = request.After is not null ? Following(candidates, anchor) : Preceding(candidates, anchor);
        }
        var ordered = request.Before is null ? Order(window) : window.OrderBy(row => row.Exact)
            .ThenByDescending(row => row.CoursePosition).ThenByDescending(row => row.Id);
        var rows = await ordered.Take(request.Limit + 1).ToListAsync(cancellationToken);
        if (rows.Count > request.Limit) rows.RemoveAt(rows.Count - 1);
        if (request.Before is not null) rows.Reverse();
        var hasPrev = rows.Count > 0 && await Preceding(candidates, rows[0]).AnyAsync(cancellationToken);
        var hasNext = rows.Count > 0 && await Following(candidates, rows[^1]).AnyAsync(cancellationToken);
        var ownStatuses = await statuses.ReadAsync(learnerId, rows.Select(row => row.Id).ToArray(), cancellationToken);
        return new(rows.Select(row => new DictionarySearchItem(row.Id, row.Lemma, row.PartOfSpeech, row.Meaning,
            row.MatchedForm, ownStatuses.GetValueOrDefault(row.Id, "not_started"))).ToArray(),
            hasNext, hasPrev, hasNext ? rows[^1].Id : null);
    }

    public async Task<DictionaryEntryCard?> GetEntryAsync(Guid learnerId, Guid entryId,
        CancellationToken cancellationToken)
    {
        // Картка слова теж обмежена активною мовою: id запису з іншої мови дає 404 (existence-hiding, AC-03).
        var language = await ResolveActiveLanguageAsync(learnerId, cancellationToken);
        var version = await (from entry in db.DictionaryEntries.AsNoTracking()
            join published in db.EntryVersions.AsNoTracking() on entry.PublishedVersionId equals published.Id
            where entry.Id == entryId && entry.TargetLanguage == language
                  && published.EntryId == entry.Id && published.ValidationStatus == "approved"
            select published).SingleOrDefaultAsync(cancellationToken);
        if (version is null) return null;
        var translations = await db.VersionTranslations.AsNoTracking().Where(row => row.VersionId == version.Id)
            .OrderBy(row => row.Position).ThenBy(row => row.Id)
            .Select(row => new WordTranslation(row.Kind, row.TextUk)).ToArrayAsync(cancellationToken);
        var ownStatuses = await statuses.ReadAsync(learnerId, [entryId], cancellationToken);
        return new(entryId, version.LemmaPl, version.PartOfSpeech, version.Gender, translations,
            version.PronunciationUk, version.ExamplePl, version.ExampleUk,
            ownStatuses.GetValueOrDefault(entryId, "not_started"));
    }

    // Активна мова learner-а; інтеримний 'pl', доки жодної мови не почато (єдиний fallback, як на гарячому шляху).
    private async Task<string> ResolveActiveLanguageAsync(Guid learnerId, CancellationToken cancellationToken) =>
        await db.Learners.AsNoTracking().Where(learner => learner.Id == learnerId)
            .Select(learner => learner.ActiveTargetLanguage).SingleOrDefaultAsync(cancellationToken) ?? "pl";

    private static IOrderedQueryable<SearchRow> Order(IQueryable<SearchRow> query) =>
        query.OrderByDescending(row => row.Exact).ThenBy(row => row.CoursePosition).ThenBy(row => row.Id);

    private static IQueryable<SearchRow> Following(IQueryable<SearchRow> query, SearchRow anchor) =>
        query.Where(row => (anchor.Exact && !row.Exact) || row.Exact == anchor.Exact &&
            (row.CoursePosition > anchor.CoursePosition || row.CoursePosition == anchor.CoursePosition && row.Id.CompareTo(anchor.Id) > 0));

    private static IQueryable<SearchRow> Preceding(IQueryable<SearchRow> query, SearchRow anchor) =>
        query.Where(row => (!anchor.Exact && row.Exact) || row.Exact == anchor.Exact &&
            (row.CoursePosition < anchor.CoursePosition || row.CoursePosition == anchor.CoursePosition && row.Id.CompareTo(anchor.Id) < 0));

    private sealed class SearchRow
    {
        public Guid Id { get; init; }
        public int CoursePosition { get; init; }
        public bool Exact { get; init; }
        public required string Lemma { get; init; }
        public required string PartOfSpeech { get; init; }
        public required string Meaning { get; init; }
        public required string MatchedForm { get; init; }
    }
}
