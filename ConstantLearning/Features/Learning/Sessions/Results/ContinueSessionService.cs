using ConstantLearning.Features.Content.Course;
using ConstantLearning.Features.Identity.LearnerDays;
using ConstantLearning.Features.Learning.Sessions.Current;
using ConstantLearning.Features.Learning.Sessions.DayBoundary;
using ConstantLearning.Features.Learning.Sessions.Start;
using ConstantLearning.Features.Learning.Sessions.Start.Limits;
using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace ConstantLearning.Features.Learning.Sessions.Results;

public sealed class ContinueSessionService(AppDbContext db, ILearnerDayStateService days, IPublishedCourseReader course,
    SessionLimitsService limits, IClock clock)
{
    public async Task<(int Status, SessionResponse? Response, SessionError? Error)> ContinueAsync(Guid learnerId, Guid sessionId, Guid itemId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var day = await days.GetForUpdateAsync(learnerId, ct);
        var session = day is null ? null : await db.LearningSessions.SingleOrDefaultAsync(s => s.Id == sessionId && s.LearnerId == learnerId, ct);
        var items = session is null ? null : await db.SessionItems.Where(i => i.SessionId == sessionId).OrderBy(i => i.Position).ToListAsync(ct);
        if (session is null || items!.All(i => i.Id != itemId || i.State != "answered"))
            return (404, null, new("session.item_not_found", "Картку не знайдено."));
        var now = clock.GetCurrentInstant();
        if (SessionExpiryGuard.CheckAndClose(session, now) is { } expired)
        {
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return (409, null, expired);
        }
        if (session.ResultItemId == itemId) session.ResultItemId = null;
        var published = await course.ReadAsync(day!.State.TargetLanguage, ct);
        var next = SessionViewBuilder.PrepareNextItem(session, items!, published.ToDictionary(w => w.EntryId), now);
        var words = await db.LearnerWords.AsNoTracking().Where(w => w.LearnerId == learnerId).ToArrayAsync(ct);
        var state = await limits.ReadAsync(day!, words, published, now, ct);
        var notices = limits.ApplyNotices(day!, state, now);
        var response = SessionResponse.From(session, items!, notices, next, await SessionResultView.ReadAsync(db, session, ct));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return (200, response, null);
    }
}
