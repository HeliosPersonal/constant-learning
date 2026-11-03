using ConstantLearning.Features.Learning.Sessions.Start;

namespace ConstantLearning.Features.Learning.Sessions.Extra;

/// <summary>Результат спроби почати extra-session: HTTP-статус і або session, або помилка-конверт.</summary>
public sealed record ExtraStartResult(int Status, SessionResponse? Session, SessionError? Error);

/// <summary>
/// Єдині визначення кодів помилок extra-дій (контракт unlimited-learning). Один код — одне визначення.
/// </summary>
public static class ExtraErrors
{
    public const string PlanNotDoneCode = "extra.plan_not_done";
    public const string CourseCompletedCode = "extra.course_completed";
    public const string NoEligibleWordsCode = "extra.no_eligible_words";
    public const string RateLimitedCode = "extra.rate_limited";

    public static SessionError PlanNotDone => new(PlanNotDoneCode, "Спершу завершіть заняття за планом.");
    public static SessionError CourseCompleted => new(CourseCompletedCode, "Ви вже вивчили всі слова курсу.");
    public static SessionError NoEligibleWords => new(NoEligibleWordsCode, "Немає слів, придатних для практики зараз.");
    public static SessionError RateLimited => new(RateLimitedCode, "Забагато дій поспіль. Спробуйте за хвилину.");
}
