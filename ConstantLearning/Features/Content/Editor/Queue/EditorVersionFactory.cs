using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ConstantLearning.Features.Content.Data;
using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace ConstantLearning.Features.Content.Editor.Queue;

internal sealed class EditorVersionFactory(AppDbContext db)
{
    public async Task<(EntryVersion? Version, EditorError? Error)> CreateAsync(DictionaryEntry entry,
        EntryVersion previous, ContentReport report, string value, Instant now, CancellationToken cancellationToken)
    {
        var translations = await db.VersionTranslations.AsNoTracking().Where(row => row.VersionId == previous.Id).ToListAsync(cancellationToken);
        var answers = await db.VersionAcceptedAnswers.AsNoTracking().Where(row => row.VersionId == previous.Id).ToListAsync(cancellationToken);
        var proofs = await db.ValidationEvidence.AsNoTracking().Where(row => row.VersionId == previous.Id).ToListAsync(cancellationToken);
        var field = report.Origin == "answer_dispute" ? "answer" : report.Field;
        if (field is not ("translation" or "pronunciation" or "lemma" or "example" or "answer")
            || value.Length is 0 || value.Length > (field == "example" ? 500 : 200) || value.Any(char.IsControl)
            || field is "translation" or "pronunciation" && (!HasUkrainian(value) || value.Contains(';'))
            || field == "pronunciation" && Regex.IsMatch(value, "[a-zA-Z]")
            || field == "lemma" && !IsLemma(value)
            || field == "answer" && !Regex.IsMatch(value, "^[a-ząćęłńóśźż]+(?:[ '-][a-ząćęłńóśźż]+)*$")
            || field == "example" && (!Regex.IsMatch(value, "[a-ząćęłńóśźżA-ZĄĆĘŁŃÓŚŹŻ]") || HasUkrainian(value)))
            return (null, Invalid("editor.invalid_value", "Остаточне значення не відповідає полю словника."));

        if (!ValidBase(entry, previous, translations, answers, proofs))
            return (null, Invalid("editor.approval_requirements_not_met", "Запис не пройшов усі перевірки для approved."));
        if (field == "lemma" && await db.DictionaryEntries.AnyAsync(row => row.Id != entry.Id
                && row.PublishedVersionId != null && row.PublishedVersion!.LemmaPl == value, cancellationToken))
            return (null, Invalid("editor.duplicate_lemma", "Така лема вже є у словнику."));
        if (field == "translation" && translations.Any(row => row.Kind != "canonical" && Normalize(row.TextUk) == Normalize(value)))
            return (null, Invalid("editor.value_already_present", "Цей переклад уже є варіантом запису."));
        if (field == "answer" && answers.Any(row => Normalize(row.TextPl) == Normalize(value))
            || field == "lemma" && previous.LemmaPl == value
            || field == "pronunciation" && Normalize(previous.PronunciationUk) == Normalize(value)
            || field == "translation" && translations.Any(row => row.Kind == "canonical" && Normalize(row.TextUk) == Normalize(value))
            || field == "example" && previous.ExamplePl == value)
            return (null, Invalid("editor.value_already_present", "Запис уже містить це значення."));

        // Редактор перевіряє відповідність значення наявному двомовному контексту; обидва приклади зберігаються.
        var createdAt = Instant.FromUnixTimeTicks(now.ToUnixTimeTicks() / 10 * 10);
        var next = new EntryVersion
        {
            EntryId = entry.Id,
            BasedOnVersionId = previous.Id,
            LemmaPl = field == "lemma" ? value : previous.LemmaPl,
            PartOfSpeech = previous.PartOfSpeech,
            Gender = previous.Gender,
            GenderPairId = previous.GenderPairId,
            AspectPairId = previous.AspectPairId,
            PronunciationUk = field == "pronunciation" ? value : previous.PronunciationUk,
            ExamplePl = field == "example" ? value : previous.ExamplePl,
            ExampleUk = previous.ExampleUk,
            ShowContextBefore = previous.ShowContextBefore,
            FrequencyRank = previous.FrequencyRank,
            UtilityRank = previous.UtilityRank,
            LegalizationStage = previous.LegalizationStage,
            Topic = previous.Topic,
            ValidationStatus = "approved",
            CreatedByKind = "editor",
            ContentReportId = report.Id,
            ReviewNote = previous.ReviewNote,
            CreatedAt = createdAt > previous.CreatedAt ? createdAt : previous.CreatedAt + Duration.FromMilliseconds(1),
            ContentHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
                new { previous.ContentHash, Field = field, Value = value, ReportId = report.Id }))))
        };
        db.EntryVersions.Add(next);
        foreach (var row in translations)
            db.VersionTranslations.Add(new VersionTranslation
            {
                VersionId = next.Id,
                Kind = row.Kind,
                Position = row.Position,
                TextUk = field == "translation" && row.Kind == "canonical" ? value : row.TextUk,
                CreatedAt = now
            });
        foreach (var row in answers)
            db.VersionAcceptedAnswers.Add(new VersionAcceptedAnswer
            {
                VersionId = next.Id,
                TextPl = row.TextPl,
                IsCanonical = field == "lemma" ? row.TextPl == value : row.IsCanonical,
                CreatedAt = now
            });
        if (field == "answer" || field == "lemma" && !answers.Any(row => row.TextPl == value))
            db.VersionAcceptedAnswers.Add(new VersionAcceptedAnswer
            {
                VersionId = next.Id,
                TextPl = value,
                IsCanonical = field == "lemma",
                CreatedAt = now
            });
        foreach (var proof in proofs)
            db.ValidationEvidence.Add(new ValidationEvidence
            {
                VersionId = next.Id,
                Field = proof.Field,
                Source = proof.Source,
                Url = proof.Url,
                RevisionId = proof.RevisionId,
                Attribution = proof.Attribution,
                Outcome = proof.Outcome,
                RetrievedAt = proof.RetrievedAt,
                CreatedAt = now
            });
        return (next, null);
    }

    private static bool ValidBase(DictionaryEntry entry, EntryVersion version, IReadOnlyList<VersionTranslation> translations,
        IReadOnlyList<VersionAcceptedAnswer> answers, IReadOnlyList<ValidationEvidence> evidence) =>
        version.ValidationStatus == "approved" && IsLemma(version.LemmaPl)
        && version.PartOfSpeech is "noun" or "verb" or "adj" or "adv" or "pron" or "det" or "prep" or "conj" or "particle" or "num" or "intj"
        && (version.PartOfSpeech != "noun" || version.Gender is "m" or "f" or "n" or "pl")
        && !string.IsNullOrWhiteSpace(version.Topic)
        && (entry.Domain == "everyday" && version.FrequencyRank is > 0
            || entry.Domain == "legalization" && version.UtilityRank is > 0
            && version.LegalizationStage is "appointment" or "application" or "supplement" or "status" or "decision" or "pickup")
        && !string.IsNullOrWhiteSpace(version.ExamplePl) && !HasUkrainian(version.ExamplePl)
        && HasUkrainian(version.ExampleUk) && HasUkrainian(version.PronunciationUk)
        && !Regex.IsMatch(version.PronunciationUk, "[a-zA-Z]")
        && translations.Count(row => row.Kind == "canonical") == 1
        && translations.All(row => !string.IsNullOrWhiteSpace(row.TextUk) && HasUkrainian(row.TextUk))
        && answers.Count(row => row.IsCanonical) == 1 && answers.Any(row => row.IsCanonical && row.TextPl == version.LemmaPl)
        && evidence.Any(proof => proof.Outcome == "confirmed" && !string.IsNullOrWhiteSpace(proof.Source)
            && Uri.TryCreate(proof.Url, UriKind.Absolute, out var url) && url.Scheme == "https"
            && !string.IsNullOrWhiteSpace(proof.Attribution));

    public static bool IsLemma(string value) => value.Length is > 0 and <= 200 && Regex.IsMatch(value, "^[a-ząćęłńóśźż]+$");
    private static bool HasUkrainian(string value) => Regex.IsMatch(value, "[а-яіїєґА-ЯІЇЄҐ]");
    private static string Normalize(string value) => value.Trim().Normalize().ToLowerInvariant();
    private static EditorError Invalid(string code, string message) => new(code, message);
}
