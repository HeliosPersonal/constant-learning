namespace ConstantLearning.Features.Learning.Domain.Composition;

/// <summary>
/// Лічильники поточної доби learner, яку визначає ILearnerDayService.
/// NewWordsShown включає слова «Вже знаю»; AlreadyKnownWords — їхня підмножина.
/// Лише перші десять «Вже знаю» не витрачають денний ліміт.
/// </summary>
public sealed record DailyNewWordUsage(int NewWordsShown, int AlreadyKnownWords)
{
    public const int FreeAlreadyKnownWords = 10;

    public int Remaining(int dailyNewWordsLimit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(dailyNewWordsLimit);
        ArgumentOutOfRangeException.ThrowIfNegative(NewWordsShown);
        ArgumentOutOfRangeException.ThrowIfNegative(AlreadyKnownWords);
        if (AlreadyKnownWords > NewWordsShown)
        {
            throw new ArgumentException("Кількість «Вже знаю» не може перевищувати кількість показаних нових слів.");
        }

        var consumed = NewWordsShown - Math.Min(AlreadyKnownWords, FreeAlreadyKnownWords);
        return Math.Max(0, dailyNewWordsLimit - consumed);
    }
}
