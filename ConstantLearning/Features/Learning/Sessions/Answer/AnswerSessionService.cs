using System.Text.Json;
using ConstantLearning.Features.Content.Learning;
using ConstantLearning.Features.Identity.LearnerDays;
using ConstantLearning.Features.Learning.Data;
using ConstantLearning.Features.Learning.Domain.Evaluation;
using ConstantLearning.Features.Learning.Domain.Scheduling;
using ConstantLearning.Features.Learning.Sessions.Current;
using ConstantLearning.Features.Learning.Sessions.DayBoundary;
using ConstantLearning.Features.Learning.Sessions.Start;
using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using NodaTime.Text;

namespace ConstantLearning.Features.Learning.Sessions.Answer;

public sealed class AnswerSessionService(AppDbContext db, ILearnerDayStateService learnerDays,
    IAnswerContentReader contentReader, IReviewScheduler scheduler, IClock clock)
{
    public async Task<AnswerSessionResult> AnswerAsync(Guid learnerId, Guid sessionId, Guid itemId,
        AnswerRequest request, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var current = await learnerDays.GetForUpdateAsync(learnerId, cancellationToken);
        if (current is null) return NotFound();
        var session = await db.LearningSessions.SingleOrDefaultAsync(value => value.Id == sessionId
            && value.LearnerId == learnerId, cancellationToken);
        var item = session is null ? null : await db.SessionItems.SingleOrDefaultAsync(value => value.Id == itemId
            && value.SessionId == sessionId, cancellationToken);
        if (session is null || item is null) return NotFound();

        var now = clock.GetCurrentInstant();
        if (SessionExpiryGuard.CheckAndClose(session, now) is { } expired)
        {
            await CommitAsync();
            return new(409, null, expired);
        }

        var hash = request.PayloadHash();
        if (item.State == "answered")
        {
            var replay = Replay(item, hash);
            if (replay.Answer is not null) await transaction.CommitAsync(cancellationToken);
            return replay;
        }
        if (session.ResultItemId is not null)
            return Error(409, "answer.result_not_continued", "Спершу перейдіть із результату до наступної картки.");
        if (item.State != "pending") return Error(409, "answer.content_unavailable", "Слово більше недоступне в цьому занятті.");
        if (session.Status != "active") return Error(409, "answer.session_inactive", "Заняття вже закрито.");
        if (item.Kind is not ("review" or "control_repetition" or "new_word_check" or "extra_practice" or "extra_new_word_check"))
            return Error(409, "answer.invalid_item_kind", "Ця картка очікує підтвердження показу, а не відповіді.");

        var content = await contentReader.ReadAsync(item.EntryId, cancellationToken);
        var published = content.Course.ToDictionary(word => word.EntryId);
        var items = await db.SessionItems.Where(value => value.SessionId == session.Id)
            .OrderBy(value => value.Position).ToArrayAsync(cancellationToken);
        var next = SessionViewBuilder.PrepareNextItem(session, items, published, now);
        if (content.Target is not { } target)
        {
            await CommitAsync();
            return Error(409, "answer.content_unavailable", "Слово більше недоступне в цьому занятті.");
        }
        if (next?.Id != item.Id) return Error(409, "answer.item_not_current", "Спершу опрацюйте поточну картку.");

        var evaluation = AnswerEvaluator.Evaluate(request.Answer, request.DontKnow,
            new EvaluationEntry(target.EntryId, target.LemmaPl, target.PromptUk, true, content.AcceptedAnswers),
            content.Course.Select(word => new EvaluationEntry(word.EntryId, word.LemmaPl, word.PromptUk, true)));
        if (!evaluation.IsEvaluated)
        {
            return Error(400, evaluation.ErrorCode!, evaluation.ErrorCode == AnswerEvaluator.EmptyAnswerCode
                ? "Введіть слово або натисніть «Не знаю»." : "Перемкніть розкладку на польську або натисніть «Не знаю».");
        }

        var word = await db.LearnerWords.SingleOrDefaultAsync(value => value.LearnerId == learnerId
            && value.EntryId == item.EntryId, cancellationToken);
        if (word?.FirstShownDay is null || word.DueOn is null || word.IntervalDays is null)
            return Error(409, "answer.word_not_ready", "Спершу перегляньте картку нового слова.");
        var before = AnswerWordState.Read(word);
        var exerciseKind = item.Kind switch
        {
            "new_word_check" or "extra_new_word_check" => ReviewExerciseKind.NewWordCheck,
            "extra_practice" => ReviewExerciseKind.ExtraPractice,
            _ => ReviewExerciseKind.ScheduledReview
        };
        // Практика (ADR-0002) не вимагає, щоб слово було до повторення, але дозволена лише раз на добу
        // і не для слова, отриманого сьогодні чи в контрольному повторенні (AC-08/AC-09).
        var notDue = exerciseKind switch
        {
            ReviewExerciseKind.NewWordCheck => before.ControlPending || before.LastReviewDay is not null
                || before.FirstShownDay != current.Day.Date,
            ReviewExerciseKind.ExtraPractice => before.ControlPending || before.FirstShownDay >= current.Day.Date
                || await db.AnswerEvents.AnyAsync(answer => answer.LearnerId == learnerId
                    && answer.EntryId == item.EntryId && answer.LearnerDay == current.Day.Date, cancellationToken),
            _ => !before.IsDue(current.Day.Date) || await db.AnswerEvents.AnyAsync(answer => answer.LearnerId == learnerId
                && answer.EntryId == item.EntryId && answer.LearnerDay == current.Day.Date
                && (answer.ExerciseKind == "review" || answer.ExerciseKind == "control_repetition"), cancellationToken)
        };
        if (notDue) return Error(409, "answer.not_due", "Це слово зараз не очікує повторення.");

        var answerEvent = new AnswerEvent
        {
            LearnerId = learnerId, EntryId = item.EntryId, VersionId = target.VersionId, SessionItemId = item.Id,
            ExerciseKind = item.Kind, Outcome = OutcomeName(evaluation.Outcome!.Value), RawAnswer = request.Answer,
            NormalizedAnswer = evaluation.NormalizedAnswer, AnsweredAt = now, LearnerDay = current.Day.Date,
            TimeZone = current.Learner.TimeZone, IntervalBefore = word.IntervalDays, SchedulerVersion = scheduler.Version,
            StateBefore = AnswerWordState.Snapshot(word), CreatedAt = now
        };
        var after = scheduler.Apply(before, new ReviewAnswer(answerEvent.Id, current.Day.Date,
            exerciseKind, evaluation.Outcome.Value, scheduler.Version));

        // Умовне захоплення страхує картку; усі наступні записи належать цій самій транзакції.
        var claimed = await db.SessionItems.Where(value => value.Id == item.Id && value.SessionId == session.Id && value.State == "pending")
            .ExecuteUpdateAsync(update => update.SetProperty(value => value.State, "answered")
                .SetProperty(value => value.AnswerPayloadHash, hash).SetProperty(value => value.AnsweredAt, now), cancellationToken);
        await db.Entry(item).ReloadAsync(cancellationToken);
        if (claimed == 0) return Replay(item, hash);

        if (after != before) AnswerWordState.Apply(word, after);
        answerEvent.DueOn = after.DueOn;
        db.AnswerEvents.Add(answerEvent);
        session.LastActivityAt = now;
        next = SessionViewBuilder.PrepareNextItem(session, items, published, now);
        var response = new AnswerResponse(answerEvent.Id, answerEvent.Outcome, evaluation.CorrectForm,
            evaluation.Diff.Select(segment => new AnswerDiffResponse(segment.Op.ToString().ToLowerInvariant(), segment.Expected, segment.Actual)).ToArray(),
            target.PronunciationUk, target.ExamplePl, target.ExampleUk, target.Gender, evaluation.DisputeAllowed,
            word.Status, LocalDatePattern.Iso.Format(after.DueOn), next, session.Status == "completed");
        item.ResponsePayload = JsonSerializer.Serialize(response);
        session.ResultItemId = item.Id;
        await CommitAsync();
        return new(200, response, null);

        async Task CommitAsync()
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
    }

    private static AnswerSessionResult Replay(SessionItem item, string hash) =>
        item.State == "answered" && item.AnswerPayloadHash == hash && item.ResponsePayload is { } payload
            ? new(200, JsonSerializer.Deserialize<AnswerResponse>(payload)
                ?? throw new InvalidOperationException("Збережений результат відповіді порожній."), null)
            : Error(409, "answer.already_answered_differently", "Цю картку вже оцінено іншою дією.");

    private static AnswerSessionResult NotFound() => Error(404, "session.item_not_found", "Картку не знайдено.");
    private static AnswerSessionResult Error(int status, string code, string message) => new(status, null, new SessionError(code, message));

    private static string OutcomeName(AnswerOutcome outcome) => outcome switch
    {
        AnswerOutcome.Correct => "correct",
        AnswerOutcome.MistakeTypo => "mistake_typo",
        AnswerOutcome.MistakeDiacritic => "mistake_diacritic",
        AnswerOutcome.DontKnow => "dont_know",
        AnswerOutcome.Synonym => "synonym",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome))
    };
}
