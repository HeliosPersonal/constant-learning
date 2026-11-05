using ConstantLearning.Features.Content.Course;
using ConstantLearning.Features.Identity.LearnerDays;
using ConstantLearning.Features.Learning.Sessions.DayBoundary;
using ConstantLearning.Features.Learning.Sessions.Start;
using ConstantLearning.Features.Learning.Sessions.Results;
using ConstantLearning.Features.Learning.Sessions.Start.Limits;
using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace ConstantLearning.Features.Learning.Sessions.Current;

public sealed class CurrentSessionService(AppDbContext db, ILearnerDayStateService learnerDays,
    SessionDayBoundaryService boundary, IPublishedCourseReader courseReader, SessionLimitsService limitsService,
    IClock clock)
{
    public async Task<SessionResponse?> GetAsync(Guid learnerId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var current = await learnerDays.GetForUpdateAsync(learnerId, cancellationToken);
        if (current is null) return null;

        var now = clock.GetCurrentInstant();
        var session = await SessionResultView.FindOpenAsync(db, learnerId, now, cancellationToken)
            ?? await boundary.GetActiveAsync(learnerId, now, cancellationToken);
        SessionResponse? response = null;
        if (session is not null)
        {
            var course = await courseReader.ReadAsync(current.State.TargetLanguage, cancellationToken);
            var items = await db.SessionItems.Where(item => item.SessionId == session.Id)
                .OrderBy(item => item.Position).ToArrayAsync(cancellationToken);
            var next = SessionViewBuilder.PrepareNextItem(session, items,
                course.ToDictionary(word => word.EntryId), now);
            if (next is not null || session.ResultItemId is not null)
            {
                var words = await db.LearnerWords.AsNoTracking().Where(word => word.LearnerId == learnerId)
                    .ToArrayAsync(cancellationToken);
                var limits = await limitsService.ReadAsync(current, words, course, now, cancellationToken);
                var notices = limitsService.ApplyNotices(current, limits, now);
                response = SessionResponse.From(session, items, notices, next, await SessionResultView.ReadAsync(db, session, cancellationToken));
            }
        }

        // Навіть 404 після межі доби комітить закриття; покази й відповіді цей GET не записує.
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return response;
    }
}
