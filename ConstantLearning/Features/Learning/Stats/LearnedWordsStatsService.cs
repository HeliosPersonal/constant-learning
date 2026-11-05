using System.Diagnostics;
using ConstantLearning.Features.Content.Search.Api;
using ConstantLearning.Features.Content.Search.Forms;
using ConstantLearning.Platform.Metrics;
using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using NodaTime.Text;

namespace ConstantLearning.Features.Learning.Stats;

/// <summary>
/// Екран «Мої слова» (learned-words-stats): читає статистику вивчених слів наживо.
/// Статус і серія/забуте — з learner_words (єдине джерело істини, як на «Сьогодні»). Показ / правильно /
/// неправильно й розбивка помилок — з answer_events за зарахованими видами вправ (review, control_repetition,
/// extra_practice) за винятком успішно оскаржених помилок (answer_neutralizations). Жодної нової таблиці.
/// Масштаб пілоту (десятки learner, сотні слів) дозволяє агрегувати в пам'яті одним прохід на запит.
/// </summary>
public sealed class LearnedWordsStatsService(AppDbContext db, TrainerMetrics metrics)
{
    private static readonly string[] GradedKinds = ["review", "control_repetition", "extra_practice"];

    public async Task<LearnedWordsPageResponse?> ListAsync(Guid learnerId, LearnedWordsStatsRequest request,
        CancellationToken ct = default)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            var (onboarded, rows) = await LoadRowsAsync(learnerId, ct);
            if (!onboarded) return null;

            var filtered = rows
                .Where(row => request.Tab == "all" || row.Status == request.Tab)
                .Where(row => Matches(row, request.FoldedQuery))
                .ToList();
            var ordered = Order(filtered, request.Sort);

            List<Row> window;
            bool hasPrev, hasNext;
            if (request.After is { } after)
            {
                var index = ordered.FindIndex(row => row.EntryId == after);
                if (index < 0) { window = []; hasPrev = true; hasNext = false; }
                else
                {
                    window = ordered.Skip(index + 1).Take(request.Limit).ToList();
                    hasPrev = true;
                    hasNext = ordered.Count > index + 1 + window.Count;
                }
            }
            else if (request.Before is { } before)
            {
                var index = ordered.FindIndex(row => row.EntryId == before);
                if (index < 0) { window = []; hasPrev = false; hasNext = true; }
                else
                {
                    var start = Math.Max(0, index - request.Limit);
                    window = ordered.Skip(start).Take(index - start).ToList();
                    hasPrev = start > 0;
                    hasNext = true;
                }
            }
            else
            {
                window = ordered.Take(request.Limit).ToList();
                hasPrev = false;
                hasNext = ordered.Count > window.Count;
            }

            Guid? nextCursor = hasNext && window.Count > 0 ? window[^1].EntryId : null;
            return new LearnedWordsPageResponse(window.Select(ToStat).ToList(), hasNext, hasPrev, nextCursor);
        }
        finally
        {
            metrics.RecordStatsQuery(Stopwatch.GetElapsedTime(started));
        }
    }

    public async Task<LearnedWordCardResponse?> GetCardAsync(Guid learnerId, Guid entryId, CancellationToken ct = default)
    {
        var (onboarded, rows) = await LoadRowsAsync(learnerId, ct);
        if (!onboarded) return null;
        var row = rows.FirstOrDefault(value => value.EntryId == entryId);
        if (row is null) return null; // слова ще не бачили — приховуємо існування (stats.word_not_found)
        return new LearnedWordCardResponse(row.EntryId, row.LemmaPl, row.TranscriptionUk, row.TranslationUk,
            row.Status, row.Shown, row.Correct, row.Incorrect, row.Streak, row.Lapses,
            Fmt(row.FirstShownDay), Fmt(row.LastReviewDay), Fmt(row.DueOn), row.IsWeak,
            row.Translations, new MistakeBreakdownResponse(row.Typo, row.Diacritic, row.DontKnow));
    }

    public async Task<LearnedWordsSummaryResponse?> SummaryAsync(Guid learnerId, CancellationToken ct = default)
    {
        var (onboarded, rows) = await LoadRowsAsync(learnerId, ct);
        if (!onboarded) return null;
        var recalled = rows.Count(row => row.Status == "recalled");
        var review = rows.Count(row => row.Status == "review");
        var weak = rows.Count(row => row.IsWeak);
        return new LearnedWordsSummaryResponse(rows.Count, recalled, review, weak);
    }

    private async Task<(bool Onboarded, List<Row> Rows)> LoadRowsAsync(Guid learnerId, CancellationToken ct)
    {
        if (!await db.Learners.AsNoTracking().AnyAsync(learner => learner.Id == learnerId, ct))
            return (false, []);

        var language = await db.Learners.AsNoTracking().Where(learner => learner.Id == learnerId)
            .Select(learner => learner.ActiveTargetLanguage).SingleAsync(ct) ?? "pl";

        var words = await db.LearnerWords.AsNoTracking()
            .Where(word => word.LearnerId == learnerId && word.TargetLanguage == language && (word.Status == "review" || word.Status == "recalled"))
            .Select(word => new
            {
                word.EntryId,
                word.Status,
                word.ConsecutiveScheduledCorrect,
                word.Lapses,
                word.FirstShownDay,
                word.LastReviewDay,
                word.DueOn
            })
            .ToListAsync(ct);
        if (words.Count == 0) return (true, []);

        var entryIds = words.Select(word => word.EntryId).Distinct().ToList();
        var display = await ResolveDisplayAsync(entryIds, ct);
        var counts = await AggregateCountsAsync(learnerId, language, ct);

        var rows = new List<Row>(words.Count);
        foreach (var word in words)
        {
            if (!display.TryGetValue(word.EntryId, out var card)) continue; // немає версії — захисно пропускаємо
            var count = counts.GetValueOrDefault(word.EntryId) ?? new Counter();
            var translationUk = card.Translations.Count > 0 ? card.Translations[0].TextUk : card.LemmaPl;
            rows.Add(new Row(word.EntryId, card.LemmaPl, card.Transcription, translationUk, card.Translations,
                word.Status, count.Shown, count.Correct, count.Incorrect, count.Typo, count.Diacritic,
                count.DontKnow, word.ConsecutiveScheduledCorrect, word.Lapses,
                word.FirstShownDay, word.LastReviewDay, word.DueOn));
        }
        return (true, rows);
    }

    private async Task<Dictionary<Guid, DisplayCard>> ResolveDisplayAsync(List<Guid> entryIds, CancellationToken ct)
    {
        var entries = await db.DictionaryEntries.AsNoTracking()
            .Where(entry => entryIds.Contains(entry.Id))
            .Select(entry => new { entry.Id, entry.PublishedVersionId })
            .ToListAsync(ct);
        var publishedByEntry = entries.ToDictionary(entry => entry.Id, entry => entry.PublishedVersionId);

        var versions = await db.EntryVersions.AsNoTracking()
            .Where(version => entryIds.Contains(version.EntryId))
            .Select(version => new { version.Id, version.EntryId, version.LemmaPl, version.PronunciationUk, version.CreatedAt })
            .ToListAsync(ct);
        var versionsByEntry = versions.GroupBy(version => version.EntryId).ToDictionary(group => group.Key, group => group.ToList());

        var chosen = new Dictionary<Guid, (Guid VersionId, string LemmaPl, string? Transcription)>();
        foreach (var entryId in entryIds)
        {
            if (!versionsByEntry.TryGetValue(entryId, out var candidates) || candidates.Count == 0) continue;
            var publishedId = publishedByEntry.GetValueOrDefault(entryId);
            var published = publishedId is { } id ? candidates.FirstOrDefault(version => version.Id == id) : null;
            var pick = published ?? candidates
                .OrderByDescending(version => version.CreatedAt).ThenByDescending(version => version.Id).First();
            // Транскрипцію показуємо лише з опублікованої версії; якщо слово втратило публікацію — null.
            chosen[entryId] = (pick.Id, pick.LemmaPl, published is not null ? pick.PronunciationUk : null);
        }

        var versionIds = chosen.Values.Select(value => value.VersionId).ToList();
        var translations = versionIds.Count == 0
            ? []
            : await db.VersionTranslations.AsNoTracking()
                .Where(translation => versionIds.Contains(translation.VersionId))
                .Select(translation => new { translation.VersionId, translation.Kind, translation.TextUk, translation.Position })
                .ToListAsync(ct);
        var translationsByVersion = translations
            .GroupBy(translation => translation.VersionId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<WordTranslation>)group
                .OrderBy(translation => translation.Kind == "canonical" ? 0 : 1)
                .ThenBy(translation => translation.Position)
                .ThenBy(translation => translation.TextUk, StringComparer.Ordinal)
                .Select(translation => new WordTranslation(translation.Kind, translation.TextUk))
                .ToList());

        return chosen.ToDictionary(pair => pair.Key, pair => new DisplayCard(pair.Value.LemmaPl, pair.Value.Transcription,
            translationsByVersion.GetValueOrDefault(pair.Value.VersionId, [])));
    }

    private async Task<Dictionary<Guid, Counter>> AggregateCountsAsync(Guid learnerId, string language, CancellationToken ct)
    {
        var events = await db.AnswerEvents.AsNoTracking()
            .Where(answer => answer.LearnerId == learnerId && answer.TargetLanguage == language && GradedKinds.Contains(answer.ExerciseKind))
            .Select(answer => new { answer.Id, answer.EntryId, answer.Outcome })
            .ToListAsync(ct);
        if (events.Count == 0) return [];

        var eventIds = events.Select(answer => answer.Id).ToList();
        var neutralized = (await db.AnswerNeutralizations.AsNoTracking()
            .Where(neutralization => eventIds.Contains(neutralization.AnswerEventId))
            .Select(neutralization => neutralization.AnswerEventId)
            .ToListAsync(ct)).ToHashSet();

        var counts = new Dictionary<Guid, Counter>();
        foreach (var answer in events)
        {
            if (!counts.TryGetValue(answer.EntryId, out var counter))
                counts[answer.EntryId] = counter = new Counter();
            counter.Shown++; // показ = будь-яка зарахована спроба незалежно від результату
            switch (answer.Outcome)
            {
                case "correct":
                    counter.Correct++;
                    break;
                case "mistake_typo" when !neutralized.Contains(answer.Id):
                    counter.Incorrect++; counter.Typo++;
                    break;
                case "mistake_diacritic" when !neutralized.Contains(answer.Id):
                    counter.Incorrect++; counter.Diacritic++;
                    break;
                case "dont_know" when !neutralized.Contains(answer.Id):
                    counter.Incorrect++; counter.DontKnow++;
                    break;
                // "synonym" та інші нейтральні результати входять лише в показ
            }
        }
        return counts;
    }

    private static bool Matches(Row row, string? foldedQuery)
    {
        if (foldedQuery is null) return true;
        if (SearchTextNormalizer.Fold(row.LemmaPl).Contains(foldedQuery, StringComparison.Ordinal)) return true;
        // Пошук за будь-яким перекладом, що показано на картці, а не лише за канонічним.
        foreach (var translation in row.Translations)
            if (SearchTextNormalizer.Fold(translation.TextUk).Contains(foldedQuery, StringComparison.Ordinal)) return true;
        return false;
    }

    private static List<Row> Order(List<Row> rows, string sort) => sort switch
    {
        "alpha" => rows
            .OrderBy(row => row.LemmaPl, StringComparer.InvariantCultureIgnoreCase)
            .ThenBy(row => row.EntryId).ToList(),
        "weakest" => rows
            .OrderByDescending(row => row.Lapses)
            .ThenByDescending(row => row.Incorrect)
            .ThenBy(row => row.SortDate)
            .ThenBy(row => row.EntryId).ToList(),
        _ => rows // recent
            .OrderByDescending(row => row.SortDate)
            .ThenBy(row => row.EntryId).ToList()
    };

    private static LearnedWordStatResponse ToStat(Row row) => new(row.EntryId, row.LemmaPl, row.TranscriptionUk,
        row.TranslationUk, row.Status, row.Shown, row.Correct, row.Incorrect, row.Streak, row.Lapses,
        Fmt(row.FirstShownDay), Fmt(row.LastReviewDay), Fmt(row.DueOn), row.IsWeak);

    private static string? Fmt(LocalDate? day) => day is { } value ? LocalDatePattern.Iso.Format(value) : null;

    private sealed class Counter
    {
        public int Shown;
        public int Correct;
        public int Incorrect;
        public int Typo;
        public int Diacritic;
        public int DontKnow;
    }

    private sealed record DisplayCard(string LemmaPl, string? Transcription, IReadOnlyList<WordTranslation> Translations);

    private sealed record Row(
        Guid EntryId, string LemmaPl, string? TranscriptionUk, string TranslationUk,
        IReadOnlyList<WordTranslation> Translations, string Status,
        int Shown, int Correct, int Incorrect, int Typo, int Diacritic, int DontKnow,
        int Streak, int Lapses, LocalDate? FirstShownDay, LocalDate? LastReviewDay, LocalDate? DueOn)
    {
        public bool IsWeak => Status != "recalled" && (Lapses > 0 || Incorrect > 0);
        public LocalDate SortDate => LastReviewDay ?? FirstShownDay ?? new LocalDate(1, 1, 1);
    }
}
