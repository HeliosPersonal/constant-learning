namespace ConstantLearning.Features.Learning.Domain.Evaluation;

public static class AnswerEvaluator
{
    public const string EmptyAnswerCode = "answer.empty";
    public const string WrongLayoutCode = "answer.wrong_layout";

    public static AnswerEvaluation Evaluate(
        string? answer,
        bool dontKnow,
        EvaluationEntry entry,
        IEnumerable<EvaluationEntry>? relatedEntries = null)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.PolishLemma);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.UkrainianTranslation);

        // Натискання «Не знаю» є окремою дією та не залежить від залишеного в полі тексту.
        if (dontKnow)
        {
            return new(AnswerOutcome.DontKnow, null, entry.PolishLemma, string.Empty, []);
        }

        var normalizedAnswer = AnswerNormalizer.Normalize(answer);

        if (normalizedAnswer.Length == 0)
        {
            return new(null, EmptyAnswerCode, entry.PolishLemma, normalizedAnswer, []);
        }

        if (AnswerNormalizer.ContainsCyrillicLetters(normalizedAnswer))
        {
            return new(null, WrongLayoutCode, entry.PolishLemma, normalizedAnswer, []);
        }

        var acceptedForms = new[] { entry.PolishLemma }
            .Concat(entry.ApprovedPolishVariants ?? [])
            .Select(form => (Original: form, Normalized: AnswerNormalizer.Normalize(form)))
            .ToArray();

        if (acceptedForms.Any(form => form.Normalized == normalizedAnswer))
        {
            return new(AnswerOutcome.Correct, null, entry.PolishLemma, normalizedAnswer, []);
        }

        if (relatedEntries?.Any(candidate => candidate.Id != entry.Id
            && candidate.IsApproved
            && string.Equals(candidate.UkrainianTranslation, entry.UkrainianTranslation, StringComparison.Ordinal)
            && AnswerNormalizer.Normalize(candidate.PolishLemma) == normalizedAnswer) == true)
        {
            return new(AnswerOutcome.Synonym, null, entry.PolishLemma, normalizedAnswer, []);
        }

        var foldedAnswer = AnswerNormalizer.FoldPolishDiacritics(normalizedAnswer);

        foreach (var form in acceptedForms)
        {
            if (AnswerNormalizer.FoldPolishDiacritics(form.Normalized) == foldedAnswer)
            {
                return new(AnswerOutcome.MistakeDiacritic, null, form.Original, normalizedAnswer,
                    SpellingDiff.Create(form.Normalized, normalizedAnswer));
            }
        }

        return new(AnswerOutcome.MistakeTypo, null, entry.PolishLemma, normalizedAnswer,
            SpellingDiff.Create(acceptedForms[0].Normalized, normalizedAnswer));
    }
}
