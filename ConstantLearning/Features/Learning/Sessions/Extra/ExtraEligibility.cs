using ConstantLearning.Features.Content.Course;
using ConstantLearning.Features.Learning.Data;
using NodaTime;

namespace ConstantLearning.Features.Learning.Sessions.Extra;

/// <summary>Доступність однієї extra-дії та причина недоступності (null, якщо дія доступна).</summary>
public sealed record ExtraAvailability(bool Available, string? Reason);

/// <summary>Серверне рішення щодо дій після виконання плану дня (AC-16, AC-17).</summary>
public sealed record ExtraActionsView(bool PlanDone, ExtraAvailability ExtraNewWord, ExtraAvailability ExtraPractice);

/// <summary>
/// Чисті правила «план дня виконано» і придатності слів для практики (unlimited-learning).
/// Лічильники й склад курсу готує викликувач під блокуванням learner; ці методи не читають БД.
/// </summary>
public static class ExtraEligibility
{
    public const string ReasonPlanNotDone = "plan_not_done";
    public const string ReasonCourseCompleted = "course_completed";
    public const string ReasonNoEligibleWords = "no_eligible_words";

    /// <summary>
    /// План дня виконано, коли немає активної session, слів до повторення й нових слів на сьогодні
    /// (ліміт вичерпано або base course завершено) — [PRD AC-16](../../../../docs/features/unlimited-learning/PRD.md).
    /// </summary>
    public static bool PlanDone(bool hasActiveSession, int dueReviewCount, bool newWordsAvailableForPlan) =>
        !hasActiveSession && dueReviewCount == 0 && !newWordsAvailableForPlan;

    public static ExtraActionsView Evaluate(bool planDone, bool courseCompleted, bool hasEligiblePracticeWords)
    {
        var newWord = !planDone ? new ExtraAvailability(false, ReasonPlanNotDone)
            : courseCompleted ? new ExtraAvailability(false, ReasonCourseCompleted)
            : new ExtraAvailability(true, null);
        var practice = !planDone ? new ExtraAvailability(false, ReasonPlanNotDone)
            : hasEligiblePracticeWords ? new ExtraAvailability(true, null)
            : new ExtraAvailability(false, ReasonNoEligibleWords);
        return new ExtraActionsView(planDone, newWord, practice);
    }

    /// <summary>
    /// Слова, придатні для додаткової практики, у порядку найближчої дати повторення (AC-08):
    /// статус «На повторення» або «Вже пригадую», схвалені, без control repetition, отримані до
    /// сьогодні й без відповіді сьогодні (AC-09, AC-11). Повертає не більше ніж <paramref name="limit"/>.
    /// </summary>
    public static IReadOnlyList<LearnerWord> EligiblePracticeWords(
        IReadOnlyList<LearnerWord> words,
        IReadOnlyDictionary<Guid, PublishedCourseWord> published,
        IReadOnlySet<Guid> answeredToday,
        LocalDate today,
        int limit = int.MaxValue) =>
        words.Where(word => IsEligible(word, published, answeredToday, today))
            .OrderBy(word => word.DueOn!.Value)
            .ThenBy(word => published[word.EntryId].CoursePosition)
            .ThenBy(word => word.EntryId)
            .Take(limit)
            .ToList();

    public static bool HasEligiblePracticeWord(
        IReadOnlyList<LearnerWord> words,
        IReadOnlyDictionary<Guid, PublishedCourseWord> published,
        IReadOnlySet<Guid> answeredToday,
        LocalDate today) =>
        words.Any(word => IsEligible(word, published, answeredToday, today));

    private static bool IsEligible(LearnerWord word, IReadOnlyDictionary<Guid, PublishedCourseWord> published,
        IReadOnlySet<Guid> answeredToday, LocalDate today) =>
        published.ContainsKey(word.EntryId)
        && word.Status is "review" or "recalled"
        && word.FirstShownDay is { } shown && shown < today
        && word.DueOn is not null && word.IntervalDays > 0
        && !word.ControlPending
        && !answeredToday.Contains(word.EntryId);
}
