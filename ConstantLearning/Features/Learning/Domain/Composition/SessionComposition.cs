namespace ConstantLearning.Features.Learning.Domain.Composition;

public enum SessionWordKind
{
    Review,
    NewWord
}

/// <summary>Одне місце session; нове слово разом із перевіркою займає одне місце.</summary>
public sealed record SessionWord(Guid EntryId, SessionWordKind Kind);

/// <summary>
/// План session без побічних ефектів. Залишок ліміту описує стан до показу запланованих карток.
/// Курс завершено, якщо у вхідному знімку вже немає не поданих схвалених лем.
/// </summary>
public sealed record SessionComposition(
    IReadOnlyList<SessionWord> Words,
    int DailyNewWordsRemaining,
    bool IsCourseFinished)
{
    public bool IsDailyLimitExhausted => DailyNewWordsRemaining == 0;
}
