using ConstantLearning.Features.Content.Course;
using ConstantLearning.Features.Identity.LearnerDays;
using ConstantLearning.Features.Learning.Sessions.Current;
using ConstantLearning.Features.Learning.Sessions.DayBoundary;
using ConstantLearning.Features.Learning.Sessions.Extra;
using ConstantLearning.Features.Learning.Sessions.Start.Limits;
using ConstantLearning.Features.Learning.Sessions.Results;
using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using NodaTime.Text;

namespace ConstantLearning.Features.Learning.Today;

public sealed class TodayService(AppDbContext db, ILearnerDayStateService learnerDays,
    SessionDayBoundaryService boundary, IPublishedCourseReader courseReader,
    SessionLimitsService limitsService, IClock clock)
{
    public async Task<TodayResponse?> GetAsync(Guid learnerId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var current = await learnerDays.GetForUpdateAsync(learnerId, cancellationToken);
        if (current is null) return null;

        var now = clock.GetCurrentInstant();
        var day = current.Day.Date;
        var session = await SessionResultView.FindOpenAsync(db, learnerId, now, cancellationToken)
            ?? await boundary.GetActiveAsync(learnerId, now, cancellationToken);
        var course = await courseReader.ReadAsync(current.State.TargetLanguage, cancellationToken);
        var published = course.ToDictionary(word => word.EntryId);
        if (session is not null)
        {
            var items = await db.SessionItems.Where(item => item.SessionId == session.Id)
                .OrderBy(item => item.Position).ToArrayAsync(cancellationToken);
            SessionViewBuilder.PrepareNextItem(session, items, published, now);
        }

        var words = await db.LearnerWords.AsNoTracking().Where(word => word.LearnerId == learnerId)
            .ToArrayAsync(cancellationToken);
        // Навіть нейтральна спроба повторення прибирає слово з черги поточної доби.
        var attempted = (await db.AnswerEvents.AsNoTracking().Where(answer => answer.LearnerId == learnerId
                && answer.LearnerDay == day
                && (answer.ExerciseKind == "review" || answer.ExerciseKind == "control_repetition"))
            .Select(answer => answer.EntryId).Distinct().ToArrayAsync(cancellationToken)).ToHashSet();
        var due = words.Count(word => published.ContainsKey(word.EntryId) && word.Status != "not_started"
            && word.FirstShownDay != null && word.IntervalDays > 0 && word.DueOn <= day
            && !attempted.Contains(word.EntryId));
        var recalled = words.Count(word => published.ContainsKey(word.EntryId) && word.Status == "recalled");
        var limits = await limitsService.ReadAsync(current, words, course, now, cancellationToken);
        var notices = limitsService.ApplyNotices(current, limits, now);
        await db.SaveChangesAsync(cancellationToken);

        // Сім календарних дат можуть охоплювати 167 або 169 годин під час зміни DST.
        var zone = DateTimeZoneProviders.Tzdb[current.Learner.TimeZone];
        var from = day.PlusDays(-6).AtStartOfDayInZone(zone).ToInstant();
        var until = day.PlusDays(1).AtStartOfDayInZone(zone).ToInstant();
        var completed = await db.LearningSessions.AsNoTracking().CountAsync(value => value.LearnerId == learnerId
            && value.Kind == "plan" && value.Status == "completed"
            && value.CompletedAt >= from && value.CompletedAt < until,
            cancellationToken);

        // Дії після плану (unlimited-learning AC-16/AC-17). План виконано, коли немає активної session,
        // слів до повторення й нових слів на сьогодні (ліміт вичерпано або base course завершено).
        var hasActive = session is not null && (session.Status == "active" || session.ResultItemId is not null);
        var newWordsForPlan = limits.NewWordsAvailable
            && limits.Usage.Remaining(current.State.DailyNewWordsLimit) > 0 && !limits.CourseCompleted;
        var planDone = ExtraEligibility.PlanDone(hasActive, due, newWordsForPlan);
        var answeredToday = (await db.AnswerEvents.AsNoTracking().Where(answer => answer.LearnerId == learnerId
                && answer.LearnerDay == day).Select(answer => answer.EntryId).Distinct()
            .ToArrayAsync(cancellationToken)).ToHashSet();
        var hasPractice = planDone && ExtraEligibility.HasEligiblePracticeWord(words, published, answeredToday, day);
        var extra = ExtraEligibility.Evaluate(planDone, limits.CourseCompleted, hasPractice);

        var response = new TodayResponse(LocalDatePattern.Iso.Format(day), due, recalled, completed,
            hasActive, limits.CourseCompleted ? "completed" : "in_progress",
            notices.Contains("course_replenished"), !notices.Contains("new_words_limit_reached"),
            ExtraActionsResponse.From(extra));
        await transaction.CommitAsync(cancellationToken);
        return response;
    }
}
