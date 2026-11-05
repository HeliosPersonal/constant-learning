using ConstantLearning.Features.Content.Course;
using ConstantLearning.Features.Identity.LearnerDays;
using ConstantLearning.Features.Learning.Data;
using ConstantLearning.Features.Learning.Domain.Composition;
using ConstantLearning.Features.Learning.Sessions.Current;
using ConstantLearning.Features.Learning.Sessions.DayBoundary;
using ConstantLearning.Features.Learning.Sessions.Results;
using ConstantLearning.Features.Learning.Sessions.Start;
using ConstantLearning.Features.Learning.Sessions.Start.Limits;
using ConstantLearning.Platform.Metrics;
using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using System.Diagnostics;

namespace ConstantLearning.Features.Learning.Sessions.Extra;

/// <summary>
/// «Ще одне слово» (unlimited-learning): додає одну картку нового слова поза денним лімітом, коли план
/// дня виконано й у курсі лишаються непоказані слова. Повторний дотик повертає активну extra-session.
/// </summary>
public sealed class StartExtraWordService(AppDbContext db, ILearnerDayStateService learnerDays,
    IPublishedCourseReader courseReader, SessionLimitsService limitsService,
    SessionDayBoundaryService boundary, IClock clock, TrainerMetrics metrics)
{
    public async Task<ExtraStartResult?> StartAsync(Guid learnerId, CancellationToken ct = default)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            return await StartCoreAsync(learnerId, ct);
        }
        finally
        {
            metrics.RecordExtraWordStart(Stopwatch.GetElapsedTime(started));
        }
    }

    private async Task<ExtraStartResult?> StartCoreAsync(Guid learnerId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var current = await learnerDays.GetForUpdateAsync(learnerId, ct);
        if (current is null) return null;

        var now = clock.GetCurrentInstant();
        var day = current.Day.Date;
        var lang = current.State.TargetLanguage;
        var session = await SessionResultView.FindOpenAsync(db, learnerId, lang, now, ct)
            ?? await boundary.GetActiveAsync(learnerId, lang, now, ct);
        var course = await courseReader.ReadAsync(current.State.TargetLanguage, ct);
        var published = course.ToDictionary(word => word.EntryId);

        if (session is not null)
        {
            // Активна extra-session (повторний дотик чи інший пристрій) повертається без створення (AC-05).
            if (session.Kind is not ("extra_word" or "extra_practice"))
                return new ExtraStartResult(409, null, ExtraErrors.PlanNotDone);
            var openItems = await db.SessionItems.Where(item => item.SessionId == session.Id)
                .OrderBy(item => item.Position).ToListAsync(ct);
            var openNext = SessionViewBuilder.PrepareNextItem(session, openItems, published, now);
            var openResponse = SessionResponse.From(session, openItems, Array.Empty<string>(), openNext,
                await SessionResultView.ReadAsync(db, session, ct));
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new ExtraStartResult(200, openResponse, null);
        }

        var words = await db.LearnerWords.AsNoTracking()
            .Where(word => word.LearnerId == learnerId && word.TargetLanguage == lang).ToArrayAsync(ct);
        var limits = await limitsService.ReadAsync(current, lang, words, course, now, ct);
        if (!await ExtraPlanGate.PlanDoneAsync(db, learnerId, lang, current, words, published, limits, ct))
            return new ExtraStartResult(409, null, ExtraErrors.PlanNotDone);
        if (await ExtraPlanGate.RateLimitedAsync(db, learnerId, now, ct))
            return new ExtraStartResult(429, null, ExtraErrors.RateLimited);

        var selected = SessionComposer.SelectNewWords(limits.Course, 1).SingleOrDefault();
        if (selected is null) return new ExtraStartResult(409, null, ExtraErrors.CourseCompleted);

        var created = new LearningSession
        {
            LearnerId = learnerId,
            TargetLanguage = lang,
            LearnerDay = day,
            Kind = "extra_word",
            EndsAt = current.Day.EndsAt,
            Status = "active",
            CreatedAt = now,
            LastActivityAt = now
        };
        var items = new List<SessionItem>
        {
            new()
            {
                SessionId = created.Id,
                EntryId = selected.EntryId,
                Position = 1,
                Kind = "new_word_card",
                State = "pending",
                CreatedAt = now
            }
        };
        db.LearningSessions.Add(created);
        db.SessionItems.AddRange(items);
        var next = SessionViewBuilder.PrepareNextItem(created, items, published, now);
        var response = SessionResponse.From(created, items, Array.Empty<string>(), next);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new ExtraStartResult(201, response, null);
    }
}
