using NodaTime;

namespace ConstantLearning.Features.Learning.Domain.Scheduling;

public enum ReviewWordStatus
{
    Review,
    Recalled
}

/// <summary>Проєкція стану однієї пари learner–слово; додаткові поля зберігаються в model_state.</summary>
public sealed record ReviewState(
    LocalDate FirstShownDay,
    LocalDate? LastReviewDay,
    LocalDate? LastScheduledAttemptDay,
    LocalDate DueOn,
    int IntervalDays,
    int ConsecutiveScheduledCorrect,
    bool ControlPending,
    int Lapses,
    double ForgettingRate,
    string SchedulerVersion)
{
    public ReviewWordStatus Status => ConsecutiveScheduledCorrect >= 2
        ? ReviewWordStatus.Recalled : ReviewWordStatus.Review;

    /// <summary>Нейтральна відповідь зберігає дату повторення, але виключає слово до наступної доби.</summary>
    public bool IsDue(LocalDate today) => DueOn <= today
        && (LastScheduledAttemptDay is null || LastScheduledAttemptDay < today);

    /// <summary>Частка прострочення; сортувати спадно, за рівності — за порядком base course.</summary>
    public double OverdueRatio(LocalDate today) =>
        Math.Max(0, Period.Between(DueOn, today, PeriodUnits.Days).Days) / (double)IntervalDays;
}
