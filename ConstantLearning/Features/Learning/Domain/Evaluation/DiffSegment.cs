namespace ConstantLearning.Features.Learning.Domain.Evaluation;

public enum DiffOperation
{
    Equal,
    Insert,
    Delete,
    Substitute
}

/// <summary>
/// Різниця нормалізованих форм: Insert додає відсутнє Expected, Delete прибирає зайве Actual.
/// Конкатенація Expected відновлює очікувану форму, Actual — нормалізовану відповідь.
/// </summary>
public sealed record DiffSegment(DiffOperation Op, string Expected, string Actual);
