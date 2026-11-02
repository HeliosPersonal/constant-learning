using System.Text.RegularExpressions;
using ConstantLearning.Features.Content.Data;
using ConstantLearning.Features.Content.References;
using NodaTime;

namespace ConstantLearning.Features.Content.Verification;

internal sealed record VerificationDecision(ReferenceCandidate? Evidence, string? Reason, bool Retry = false);

internal static class VerificationPolicy
{
    public static bool IsEligible(ContentReport report) => report.Status == "pending_verification"
        && report.Origin == "card" && report.EntryId is not null && report.VersionId is not null
        && report.Field is "translation" or "pronunciation" && !string.IsNullOrWhiteSpace(report.ProposedValue);

    public static bool IsTransient(string? reason) => reason is "source_unavailable" or "source_timeout"
        or "source_rate_limited" or "worker_failure";

    public static string? Validate(EntryVersion version, DictionaryEntry entry, ContentReport report,
        IReadOnlyList<VersionTranslation> translations, IReadOnlyList<VersionAcceptedAnswer> answers,
        IReadOnlyList<ValidationEvidence> evidence)
    {
        var proposed = report.ProposedValue!.Trim().Normalize();
        if (version.ValidationStatus != "approved" || proposed.Length is 0 or > 200
            || proposed.Any(char.IsControl) || proposed.Contains(';')) return "invalid_proposed_value";
        if (report.Field == "translation" && version.ShowContextBefore) return "context_requires_editor";
        if (!Regex.IsMatch(version.LemmaPl, "^[a-ząćęłńóśźż]+$")
            || version.PartOfSpeech is not ("noun" or "verb" or "adj" or "adv" or "pron" or "det"
                or "prep" or "conj" or "particle" or "num" or "intj")
            || version.PartOfSpeech == "noun" && version.Gender is not ("m" or "f" or "n" or "pl")
            || string.IsNullOrWhiteSpace(version.Topic)
            || entry.Domain is not ("everyday" or "legalization")
            || entry.Domain == "everyday" && version.FrequencyRank is not > 0
            || entry.Domain == "legalization" && (version.UtilityRank is not > 0
                || version.LegalizationStage is not ("appointment" or "application" or "supplement" or "status" or "decision" or "pickup"))
            || string.IsNullOrWhiteSpace(version.ExamplePl) || string.IsNullOrWhiteSpace(version.ExampleUk)
            || !HasUkrainian(version.ExampleUk) || Regex.IsMatch(version.ExamplePl, "[а-яіїєґА-ЯІЇЄҐ]")
            || string.IsNullOrWhiteSpace(version.PronunciationUk)
            || Regex.IsMatch(version.PronunciationUk, "[a-zA-Z]")
            || translations.Count(value => value.Kind == "canonical") != 1
            || translations.Any(value => string.IsNullOrWhiteSpace(value.TextUk) || !HasUkrainian(value.TextUk))
            || !answers.Any(answer => answer.IsCanonical && answer.TextPl == version.LemmaPl)
            || evidence.Count == 0 || !evidence.Any(proof => proof.Outcome == "confirmed"))
            return "approval_requirements_not_met";
        if (report.Field == "translation" && !HasUkrainian(proposed)
            || report.Field == "pronunciation" && (!HasUkrainian(proposed) || Regex.IsMatch(proposed, "[a-zA-Z]")))
            return "invalid_proposed_language";
        if (report.Field == "translation" && translations.Any(value => Normalize(value.TextUk) == Normalize(proposed))
            || report.Field == "pronunciation" && Normalize(version.PronunciationUk) == Normalize(proposed))
            return "value_already_present";
        return null;
    }

    public static VerificationDecision Decide(ContentReport report, DictionaryEntry entry,
        IReadOnlyList<(string Source, ReferenceResult Result)> results, Instant now)
    {
        // Навіть кандидат із ручної відповіді може виявити розбіжність вимови між джерелами.
        var all = results.SelectMany(result => result.Result.Candidates).ToArray();
        if (report.Field == "pronunciation" && all.Select(candidate => Normalize(candidate.Value)).Distinct().Count() > 1)
            return new(null, "source_conflict");
        var proposed = Normalize(report.ProposedValue!);
        foreach (var (source, result) in results.Where(item => item.Result.Status == ReferenceStatus.Available))
        {
            var match = result.Candidates.FirstOrDefault(candidate => Normalize(candidate.Value) == proposed
                && ValidEvidence(candidate, source, entry.Domain, report.Field!, now));
            if (match is not null) return new(match, null);
        }
        var transient = results.Select(item => item.Result.Reason).FirstOrDefault(IsTransient);
        if (transient is not null) return new(null, transient, true);
        return new(null, results.Select(item => item.Result.Reason).FirstOrDefault(reason => reason is not null)
                         ?? "value_not_confirmed");
    }

    private static bool ValidEvidence(ReferenceCandidate candidate, string source, string domain, string field, Instant now)
    {
        if (candidate.Source != source || candidate.SnapshotAt > now || now - candidate.SnapshotAt > Duration.FromDays(35)
            || string.IsNullOrWhiteSpace(candidate.Attribution) || candidate.Attribution.Length > 200
            || !Uri.TryCreate(candidate.Url, UriKind.Absolute, out var url) || url.Scheme != "https"
            || !url.IsDefaultPort || url.UserInfo.Length != 0) return false;
        if (field == "translation" && domain == "legalization")
            return source == "government" && (url.Host == "gov.pl" || url.Host.EndsWith(".gov.pl", StringComparison.Ordinal));
        if (source == "wikislownik")
            return url.Host == "pl.wiktionary.org" && candidate.RevisionId is { Length: > 0 and <= 32 }
                && !string.IsNullOrWhiteSpace(candidate.License);
        return field == "pronunciation" && source == "wsjp" && url.Host == "wsjp.pl"
            && candidate.RevisionId is null or { Length: <= 32 };
    }

    private static bool HasUkrainian(string value) => Regex.IsMatch(value, "[а-яіїєґА-ЯІЇЄҐ]");
    private static string Normalize(string value) => value.Trim().Normalize().ToLowerInvariant();
}
