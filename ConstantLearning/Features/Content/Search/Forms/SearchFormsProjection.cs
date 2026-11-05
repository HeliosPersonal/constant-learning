using ConstantLearning.Features.Content.Data;
using ConstantLearning.Features.Content.Publishing;
using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace ConstantLearning.Features.Content.Search.Forms;

public sealed class SearchFormsProjection(AppDbContext db, IClock clock) : IPublicationProjection
{
    // Мова пояснень — завжди українська (uk-переклади присутні для будь-якої цільової мови); це не
    // дискримінатор цільової мови, тож лишається константою, а не береться з контексту запису.
    private const string TranslationLang = "uk";

    public async Task UpdateAsync(IReadOnlyList<PublicationChange> changes, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Пошукова проєкція потребує транзакції публікації.");
        var entryIds = changes.Select(change => change.EntryId).Distinct().ToArray();
        // Видалення та вставка стають видимими лише разом зі зміною вказівника публікації.
        await db.SearchForms.Where(form => entryIds.Contains(form.EntryId)).ExecuteDeleteAsync(cancellationToken);
        foreach (var tracked in db.ChangeTracker.Entries<SearchForm>().Where(item => entryIds.Contains(item.Entity.EntryId)).ToArray())
            tracked.State = EntityState.Detached;
        // Мова кожного запису — з колонки target_language (ADR-0003), а не з константи: партія може бути змішаною.
        var entryLanguages = await db.DictionaryEntries.AsNoTracking().Where(entry => entryIds.Contains(entry.Id))
            .ToDictionaryAsync(entry => entry.Id, entry => entry.TargetLanguage, cancellationToken);
        var versions = await db.EntryVersions.AsNoTracking()
            .Where(version => entryIds.Contains(version.EntryId) && version.Entry.PublishedVersionId == version.Id
                && version.ValidationStatus == "approved").ToListAsync(cancellationToken);
        var versionIds = versions.Select(version => version.Id).ToArray();
        var answers = await db.VersionAcceptedAnswers.AsNoTracking().Where(answer => versionIds.Contains(answer.VersionId))
            .ToListAsync(cancellationToken);
        var translations = await db.VersionTranslations.AsNoTracking().Where(value => versionIds.Contains(value.VersionId))
            .ToListAsync(cancellationToken);
        var seeds = versions.Select(version => SearchTextNormalizer.Normalize(version.LemmaPl))
            .Concat(answers.Select(answer => SearchTextNormalizer.Normalize(answer.TextPl))).Distinct().ToArray();
        var langs = entryLanguages.Values.Distinct().ToArray();
        // Морфологія обирається за (мова запису, лема): ключ (Lang, Lemma) не дає формам іншої мови просочитися.
        var morphology = (await db.MorphForms.AsNoTracking()
                .Where(form => langs.Contains(form.Lang) && seeds.Contains(form.Lemma)).ToListAsync(cancellationToken))
            .ToLookup(form => (form.Lang, form.Lemma));
        var answersByVersion = answers.ToLookup(answer => answer.VersionId);
        var translationsByVersion = translations.ToLookup(value => value.VersionId);
        foreach (var version in versions)
        {
            var lang = entryLanguages[version.EntryId];
            var forms = new Dictionary<(string Lang, string Display), SearchForm>();
            void Add(string formLang, string display, bool isLemma = false)
            {
                display = display.Trim().Normalize();
                var key = (formLang, display);
                if (forms.TryGetValue(key, out var existing)) { existing.IsLemma |= isLemma; return; }
                forms.Add(key, new SearchForm
                {
                    EntryId = version.EntryId, VersionId = version.Id, Lang = formLang, FormDisplay = display,
                    FormFolded = SearchTextNormalizer.Fold(display), IsLemma = isLemma, CreatedAt = clock.GetCurrentInstant()
                });
            }
            Add(lang, version.LemmaPl, true);
            var versionAnswers = answersByVersion[version.Id].ToArray();
            foreach (var answer in versionAnswers) Add(lang, answer.TextPl, answer.IsCanonical);
            foreach (var seed in versionAnswers.Select(answer => answer.TextPl).Append(version.LemmaPl)
                .Select(SearchTextNormalizer.Normalize).Distinct())
                foreach (var form in morphology[(lang, seed)]) Add(lang, form.Form);
            // Українські морфологічні таблиці навмисно не використовуємо до рішення про VESUM.
            foreach (var translation in translationsByVersion[version.Id]) Add(TranslationLang, translation.TextUk, true);
            db.SearchForms.AddRange(forms.Values);
        }
    }

    internal async Task<int> RebuildInTransactionAsync(bool onlyMissing, CancellationToken cancellationToken)
    {
        await db.SearchForms.Where(form => !db.DictionaryEntries.Any(entry => entry.Id == form.EntryId
                && entry.PublishedVersionId == form.VersionId && entry.PublishedVersion!.ValidationStatus == "approved"))
            .ExecuteDeleteAsync(cancellationToken);
        var entries = await db.DictionaryEntries.AsNoTracking()
            .Where(entry => entry.PublishedVersionId != null && entry.PublishedVersion!.ValidationStatus == "approved"
                && (!onlyMissing || !db.SearchForms.Any(form => form.EntryId == entry.Id
                    && form.VersionId == entry.PublishedVersionId && form.Lang == entry.TargetLanguage && form.IsLemma)))
            .Select(entry => new { entry.Id, entry.PublishedVersionId }).ToListAsync(cancellationToken);
        foreach (var batch in entries.Chunk(200))
        {
            await UpdateAsync(batch.Select(entry => new PublicationChange(entry.Id, entry.PublishedVersionId,
                entry.PublishedVersionId, "import")).ToArray(), cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            foreach (var form in db.ChangeTracker.Entries<SearchForm>().ToArray())
                form.State = EntityState.Detached;
        }
        return entries.Count;
    }
}
