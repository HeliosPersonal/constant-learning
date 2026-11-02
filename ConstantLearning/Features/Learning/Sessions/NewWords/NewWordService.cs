using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ConstantLearning.Features.Content.Course;
using ConstantLearning.Features.Identity.LearnerDays;
using ConstantLearning.Features.Learning.Data;
using ConstantLearning.Features.Learning.Domain.Composition;
using ConstantLearning.Features.Learning.Domain.Scheduling;
using ConstantLearning.Features.Learning.Sessions.Answer;
using ConstantLearning.Features.Learning.Sessions.Current;
using ConstantLearning.Features.Learning.Sessions.DayBoundary;
using ConstantLearning.Features.Learning.Sessions.Start;
using ConstantLearning.Features.Learning.Sessions.Start.Limits;
using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using NodaTime.Text;

namespace ConstantLearning.Features.Learning.Sessions.NewWords;

public sealed class NewWordService(AppDbContext db, ILearnerDayStateService learnerDays,
    IPublishedCourseReader courseReader, SessionLimitsService limitsService, IReviewScheduler scheduler, IClock clock)
{
    public async Task<NewWordActionResult> ActAsync(Guid learnerId, Guid sessionId, Guid itemId,
        bool alreadyKnown, CancellationToken cancellationToken = default)
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

        var hash = PayloadHash(alreadyKnown);
        if (item.State == "answered")
        {
            var replay = Replay(item, hash, alreadyKnown);
            if (replay.Response is not null) await transaction.CommitAsync(cancellationToken);
            return replay;
        }
        if (item.State != "pending") return Unavailable();
        if (session.Status != "active") return Error("new_word.session_inactive", "Заняття вже закрито.");
        if (item.Kind != "new_word_card") return Error("new_word.invalid_item_kind", "Ця картка очікує відповіді на вправу.");

        var course = await courseReader.ReadAsync(cancellationToken);
        var published = course.ToDictionary(word => word.EntryId);
        var items = await db.SessionItems.Where(value => value.SessionId == session.Id)
            .OrderBy(value => value.Position).ToListAsync(cancellationToken);
        var next = SessionViewBuilder.PrepareNextItem(session, items, published, now);
        if (!published.TryGetValue(item.EntryId, out var target))
        {
            await CommitAsync();
            return Unavailable();
        }
        if (next?.Id != item.Id) return Error("new_word.item_not_current", "Спершу опрацюйте поточну картку.");

        var words = await db.LearnerWords.Where(value => value.LearnerId == learnerId).ToListAsync(cancellationToken);
        var limits = await limitsService.ReadAsync(current, words, course, now, cancellationToken);
        if (!limits.NewWordsAvailable || limits.Usage.Remaining(current.Learner.DailyNewWordsLimit) == 0)
            return Error("new_word.limit_reached", "Нові слова стануть доступні наступної навчальної доби.");
        var word = words.SingleOrDefault(value => value.EntryId == item.EntryId);
        if (word?.FirstShownDay is not null || limits.Course.Single(value => value.EntryId == item.EntryId).IsPresented)
            return Error("new_word.already_presented", "Це слово вже опрацьовано.");

        var claimed = await db.SessionItems.Where(value => value.Id == item.Id && value.SessionId == session.Id && value.State == "pending")
            .ExecuteUpdateAsync(update => update.SetProperty(value => value.State, "answered")
                .SetProperty(value => value.AnswerPayloadHash, hash).SetProperty(value => value.AnsweredAt, now), cancellationToken);
        await db.Entry(item).ReloadAsync(cancellationToken);
        if (claimed == 0) return Replay(item, hash, alreadyKnown);

        if (word is null)
        {
            word = new LearnerWord
            {
                LearnerId = learnerId,
                EntryId = item.EntryId,
                Status = "not_started",
                ModelState = "{}",
                SchedulerVersion = scheduler.Version,
                CreatedAt = now
            };
            db.LearnerWords.Add(word);
            words.Add(word);
        }
        var before = AnswerWordState.Snapshot(word);
        var introduced = scheduler.Introduce(current.Day.Date, alreadyKnown);
        AnswerWordState.Apply(word, introduced);
        if (alreadyKnown)
        {
            db.AnswerEvents.Add(new AnswerEvent
            {
                LearnerId = learnerId,
                EntryId = item.EntryId,
                VersionId = target.VersionId,
                SessionItemId = item.Id,
                ExerciseKind = "new_word_card",
                Outcome = "already_known",
                AnsweredAt = now,
                LearnerDay = current.Day.Date,
                TimeZone = current.Learner.TimeZone,
                DueOn = introduced.DueOn,
                SchedulerVersion = scheduler.Version,
                StateBefore = before,
                CreatedAt = now
            });
        }
        session.LastActivityAt = now;
        // Наступне читання квоти має бачити і показ, і already_known; транзакція ще не завершена.
        await db.SaveChangesAsync(cancellationToken);
        limits = await limitsService.ReadAsync(current, words, course, now, cancellationToken);

        var replacementAdded = false;
        if (alreadyKnown && limits.Usage.AlreadyKnownWords <= DailyNewWordUsage.FreeAlreadyKnownWords)
        {
            var pending = items.Where(value => value.State == "pending" && value.Kind == "new_word_card")
                .Select(value => value.EntryId).ToHashSet();
            if (limits.Usage.Remaining(current.Learner.DailyNewWordsLimit) > pending.Count)
            {
                // Заплановані картки резервують свої частки квот і не можуть стати власною заміною.
                var reserved = limits.Course.Select(value => pending.Contains(value.EntryId)
                    ? value with { IsPresented = true } : value);
                var replacement = SessionComposer.SelectNewWords(reserved, 1).SingleOrDefault();
                if (replacement is not null)
                {
                    var position = checked((short)(items.Where(value => value.Kind != "new_word_check").Max(value => value.Position) + 1));
                    AddItem(replacement.EntryId, "new_word_card", position);
                    replacementAdded = true;
                }
            }
        }
        else if (!alreadyKnown)
        {
            // Окремий діапазон залишає всі картки показу та до десяти замін перед перевірками.
            var firstCheck = SessionComposer.MaximumSessionWords + DailyNewWordUsage.FreeAlreadyKnownWords + 1;
            var position = checked((short)Math.Max(firstCheck,
                items.Where(value => value.Kind == "new_word_check").Select(value => (int)value.Position).DefaultIfEmpty(0).Max() + 1));
            AddItem(item.EntryId, "new_word_check", position);
        }

        next = SessionViewBuilder.PrepareNextItem(session, items, published, now);
        object response = alreadyKnown
            ? new AlreadyKnownResponse(LocalDatePattern.Iso.Format(introduced.DueOn), replacementAdded, next, session.Status == "completed")
            : new SessionProgressResponse(next, session.Status == "completed");
        item.ResponsePayload = JsonSerializer.Serialize(response, response.GetType());
        await CommitAsync();
        return new(200, response, null);

        void AddItem(Guid entryId, string kind, short position)
        {
            var added = new SessionItem
            {
                SessionId = session.Id,
                EntryId = entryId,
                Kind = kind,
                Position = position,
                State = "pending",
                CreatedAt = now
            };
            items.Add(added);
            db.SessionItems.Add(added);
        }

        async Task CommitAsync()
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
    }

    private static string PayloadHash(bool alreadyKnown) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(alreadyKnown ? "{\"action\":\"already_known\"}" : "{\"action\":\"acknowledge\"}")));

    private static NewWordActionResult Replay(SessionItem item, string hash, bool alreadyKnown)
    {
        if (item.State != "answered" || item.AnswerPayloadHash != hash || item.ResponsePayload is not { } payload)
            return Error("new_word.already_processed_differently", "Цю картку вже опрацьовано іншою дією.");
        object response = alreadyKnown
            ? JsonSerializer.Deserialize<AlreadyKnownResponse>(payload) ?? throw new InvalidOperationException("Збережений результат порожній.")
            : JsonSerializer.Deserialize<SessionProgressResponse>(payload) ?? throw new InvalidOperationException("Збережений результат порожній.");
        return new(200, response, null);
    }

    private static NewWordActionResult NotFound() => new(404, null, new SessionError("session.item_not_found", "Картку не знайдено."));
    private static NewWordActionResult Unavailable() => Error("new_word.content_unavailable", "Слово більше недоступне в цьому занятті.");
    private static NewWordActionResult Error(string code, string message) => new(409, null, new SessionError(code, message));
}
