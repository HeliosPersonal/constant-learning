using ConstantLearning.Features.Content.Course;
using ConstantLearning.Features.Identity.LearnerDays;
using ConstantLearning.Features.Learning.Data;
using ConstantLearning.Features.Learning.Domain.Composition;
using ConstantLearning.Features.Learning.Domain.Scheduling;
using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace ConstantLearning.Features.Learning.Sessions.Start;

public sealed record StartSessionResult(bool Created, SessionResponse Session);

public sealed class StartSessionService(AppDbContext db, ILearnerDayStateService learnerDays,
    IPublishedCourseReader courseReader, IClock clock)
{
    public async Task<StartSessionResult?> StartAsync(Guid learnerId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var current = await learnerDays.GetForUpdateAsync(learnerId, cancellationToken);
        if (current is null) return null;

        var now = clock.GetCurrentInstant();
        // Звільняємо частковий унікальний індекс до INSERT нової session у цій самій транзакції.
        await db.LearningSessions.Where(session => session.LearnerId == learnerId
                && session.Status == "active" && session.EndsAt <= now)
            .ExecuteUpdateAsync(update => update.SetProperty(session => session.Status, "incomplete"), cancellationToken);

        var course = await courseReader.ReadAsync(cancellationToken);
        var published = course.ToDictionary(word => word.EntryId);
        var words = await db.LearnerWords.AsNoTracking().Where(word => word.LearnerId == learnerId)
            .ToArrayAsync(cancellationToken);
        var presented = words.Where(word => word.FirstShownDay != null).Select(word => word.EntryId).ToHashSet();
        var knownToday = await db.AnswerEvents.Where(answer => answer.LearnerId == learnerId
                && answer.LearnerDay == current.Day.Date && answer.Outcome == "already_known")
            .Select(answer => answer.EntryId).Distinct().ToArrayAsync(cancellationToken);
        var shownToday = words.Where(word => word.FirstShownDay == current.Day.Date)
            .Select(word => word.EntryId).Union(knownToday).Count();
        presented.UnionWith(knownToday);
        var usage = new DailyNewWordUsage(shownToday, knownToday.Length);
        var available = current.Learner.NewWordsAvailableFrom is null || current.Learner.NewWordsAvailableFrom <= now;
        var courseCandidates = course.Select(word => new CourseWordCandidate(word.EntryId,
            word.Domain == "everyday" ? DomainQuota.Everyday : DomainQuota.Legalization,
            word.Rank, word.CoursePosition, true, presented.Contains(word.EntryId))).ToArray();

        var session = await db.LearningSessions.SingleOrDefaultAsync(value => value.LearnerId == learnerId
            && value.Status == "active", cancellationToken);
        var created = session is null;
        List<SessionItem> items;
        if (session is null)
        {
            var attemptedToday = await db.AnswerEvents.Where(answer => answer.LearnerId == learnerId
                    && answer.LearnerDay == current.Day.Date
                    && (answer.ExerciseKind == "review" || answer.ExerciseKind == "control_repetition"))
                .Select(answer => answer.EntryId).Distinct().ToArrayAsync(cancellationToken);
            var attempted = attemptedToday.ToHashSet();
            var reviews = words.Where(word => word.Status != "not_started" && word.FirstShownDay != null
                    && word.DueOn != null && word.IntervalDays > 0 && published.ContainsKey(word.EntryId))
                .Select(word => new ReviewCandidate(word.EntryId, published[word.EntryId].CoursePosition,
                    new ReviewState(word.FirstShownDay!.Value, word.LastReviewDay,
                        attempted.Contains(word.EntryId) ? current.Day.Date : null,
                        word.DueOn!.Value, word.IntervalDays!.Value, word.ConsecutiveScheduledCorrect,
                        word.ControlPending, word.Lapses, 0.3, word.SchedulerVersion)));
            var composition = SessionComposer.Compose(current.Day.Date, reviews, courseCandidates,
                current.Learner.DailyNewWordsLimit, usage, available);
            session = new LearningSession
            {
                LearnerId = learnerId,
                LearnerDay = current.Day.Date,
                EndsAt = current.Day.EndsAt,
                Status = composition.Words.Count == 0 ? "completed" : "active",
                CompletedAt = composition.Words.Count == 0 ? now : null,
                CreatedAt = now,
                LastActivityAt = now
            };
            var controls = words.Where(word => word.ControlPending).Select(word => word.EntryId).ToHashSet();
            items = composition.Words.Select((word, index) => new SessionItem
            {
                SessionId = session.Id,
                EntryId = word.EntryId,
                Position = (short)(index + 1),
                Kind = word.Kind == SessionWordKind.NewWord ? "new_word_card"
                    : controls.Contains(word.EntryId) ? "control_repetition" : "review",
                State = "pending",
                CreatedAt = now
            }).ToList();
            db.LearningSessions.Add(session);
            db.SessionItems.AddRange(items);
        }
        else
        {
            items = await db.SessionItems.Where(item => item.SessionId == session.Id)
                .OrderBy(item => item.Position).ToListAsync(cancellationToken);
        }

        // Вилучений зі схваленого курсу вміст не потрапляє на картку навіть у вже створеній session.
        foreach (var item in items.Where(item => item.State == "pending" && !published.ContainsKey(item.EntryId)))
        {
            item.State = "skipped";
        }

        var next = items.FirstOrDefault(item => item.State == "pending");
        if (next is null && session.Status == "active")
        {
            session.Status = "completed";
            session.CompletedAt = now;
        }

        var notices = new List<string>();
        if (!available || usage.Remaining(current.Learner.DailyNewWordsLimit) == 0) notices.Add("new_words_limit_reached");
        if (!courseCandidates.Any(word => !word.IsPresented)) notices.Add("course_completed");
        var response = SessionResponse.From(session, items, notices,
            next is null ? null : SessionItemResponse.From(next, published[next.EntryId]));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new StartSessionResult(created, response);
    }
}
