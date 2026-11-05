using ConstantLearning.Features.Content.Course;
using ConstantLearning.Features.Identity.LearnerDays;
using ConstantLearning.Features.Learning.Data;
using ConstantLearning.Features.Learning.Domain.Composition;
using ConstantLearning.Features.Learning.Domain.Scheduling;
using ConstantLearning.Features.Learning.Sessions.Current;
using ConstantLearning.Features.Learning.Sessions.DayBoundary;
using ConstantLearning.Features.Learning.Sessions.Start.Limits;
using ConstantLearning.Features.Learning.Sessions.Results;
using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace ConstantLearning.Features.Learning.Sessions.Start;

public sealed record StartSessionResult(bool Created, SessionResponse Session);

public sealed class StartSessionService(AppDbContext db, ILearnerDayStateService learnerDays,
    IPublishedCourseReader courseReader, SessionLimitsService limitsService, SessionDayBoundaryService boundary, IClock clock)
{
    public async Task<StartSessionResult?> StartAsync(Guid learnerId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var current = await learnerDays.GetForUpdateAsync(learnerId, cancellationToken);
        if (current is null) return null;

        var now = clock.GetCurrentInstant();
        var session = await SessionResultView.FindOpenAsync(db, learnerId, now, cancellationToken)
            ?? await boundary.GetActiveAsync(learnerId, now, cancellationToken);

        // Початок заняття за планом закриває активну extra-session як незавершену (ADR-0001). Зміну
        // скидаємо окремим SaveChanges, щоб звільнити частковий UNIQUE активної session до вставки плану.
        if (session is not null && session.Kind != "plan")
        {
            session.Status = "incomplete";
            session.CompletedAt = null;
            session.ResultItemId = null;
            await db.SaveChangesAsync(cancellationToken);
            session = null;
        }

        var course = await courseReader.ReadAsync(cancellationToken);
        var published = course.ToDictionary(word => word.EntryId);
        var words = await db.LearnerWords.AsNoTracking().Where(word => word.LearnerId == learnerId)
            .ToArrayAsync(cancellationToken);
        var limits = await limitsService.ReadAsync(current, words, course, now, cancellationToken);

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
            var composition = SessionComposer.Compose(current.Day.Date, reviews, limits.Course,
                current.State.DailyNewWordsLimit, limits.Usage, limits.NewWordsAvailable);
            session = new LearningSession
            {
                LearnerId = learnerId,
                LearnerDay = current.Day.Date,
                Kind = "plan",
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

        var next = SessionViewBuilder.PrepareNextItem(session, items, published, now);
        var notices = limitsService.ApplyNotices(current, limits, now);
        var response = SessionResponse.From(session, items, notices, next, await SessionResultView.ReadAsync(db, session, cancellationToken));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new StartSessionResult(created, response);
    }
}
