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
/// «Потренувати вивчені» (unlimited-learning): до 20 вивчених слів у порядку найближчого повторення, коли
/// план дня виконано. Правильні відповіді не зсувають розклад (ADR-0002). Повторний дотик повертає активну.
/// </summary>
public sealed class StartExtraPracticeService(AppDbContext db, ILearnerDayStateService learnerDays,
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
            metrics.RecordExtraPracticeStart(Stopwatch.GetElapsedTime(started));
        }
    }

    private async Task<ExtraStartResult?> StartCoreAsync(Guid learnerId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var current = await learnerDays.GetForUpdateAsync(learnerId, ct);
        if (current is null) return null;

        var now = clock.GetCurrentInstant();
        var day = current.Day.Date;
        var session = await SessionResultView.FindOpenAsync(db, learnerId, now, ct)
            ?? await boundary.GetActiveAsync(learnerId, now, ct);
        var course = await courseReader.ReadAsync(current.State.TargetLanguage, ct);
        var published = course.ToDictionary(word => word.EntryId);

        if (session is not null)
        {
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

        var words = await db.LearnerWords.AsNoTracking().Where(word => word.LearnerId == learnerId).ToArrayAsync(ct);
        var limits = await limitsService.ReadAsync(current, words, course, now, ct);
        if (!await ExtraPlanGate.PlanDoneAsync(db, learnerId, current, words, published, limits, ct))
            return new ExtraStartResult(409, null, ExtraErrors.PlanNotDone);
        if (await ExtraPlanGate.RateLimitedAsync(db, learnerId, now, ct))
            return new ExtraStartResult(429, null, ExtraErrors.RateLimited);

        var answeredToday = (await db.AnswerEvents.AsNoTracking().Where(answer => answer.LearnerId == learnerId
                && answer.LearnerDay == day).Select(answer => answer.EntryId).Distinct().ToArrayAsync(ct)).ToHashSet();
        var eligible = ExtraEligibility.EligiblePracticeWords(words, published, answeredToday, day,
            SessionComposer.MaximumSessionWords);
        if (eligible.Count == 0) return new ExtraStartResult(409, null, ExtraErrors.NoEligibleWords);

        var created = new LearningSession
        {
            LearnerId = learnerId,
            LearnerDay = day,
            Kind = "extra_practice",
            EndsAt = current.Day.EndsAt,
            Status = "active",
            CreatedAt = now,
            LastActivityAt = now
        };
        var items = eligible.Select((word, index) => new SessionItem
        {
            SessionId = created.Id,
            EntryId = word.EntryId,
            Position = (short)(index + 1),
            Kind = "extra_practice",
            State = "pending",
            CreatedAt = now
        }).ToList();
        db.LearningSessions.Add(created);
        db.SessionItems.AddRange(items);
        var next = SessionViewBuilder.PrepareNextItem(created, items, published, now);
        var response = SessionResponse.From(created, items, Array.Empty<string>(), next);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new ExtraStartResult(201, response, null);
    }
}
