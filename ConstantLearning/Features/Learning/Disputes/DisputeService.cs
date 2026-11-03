using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ConstantLearning.Features.Content.Reports;
using ConstantLearning.Features.Identity.LearnerDays;
using ConstantLearning.Features.Learning.Data;
using ConstantLearning.Features.Learning.Sessions.Start;
using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using NodaTime.Text;

namespace ConstantLearning.Features.Learning.Disputes;

public sealed record DisputeResult(
    [property: JsonPropertyName("outcome")] string Outcome,
    [property: JsonPropertyName("content_report_id")] Guid ContentReportId,
    [property: JsonPropertyName("message")] string Message)
{
    public static DisputeResult From(Guid id) => new("neutralized", id, "Дякуємо, передали на перевірку");
}

public sealed class DisputeService(AppDbContext db, ILearnerDayStateService days, IContentReportService reports, IClock clock)
{
    public async Task<(int Status, DisputeResult? Result, SessionError? Error)> DisputeAsync(Guid learnerId, Guid answerId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var day = await days.GetForUpdateAsync(learnerId, ct);
        var answer = day is null ? null : await db.AnswerEvents.SingleOrDefaultAsync(a => a.Id == answerId && a.LearnerId == learnerId, ct);
        if (answer is null) return (404, null, new("answer.not_found", "Відповідь не знайдено."));
        var previous = await db.AnswerNeutralizations.SingleOrDefaultAsync(n => n.AnswerEventId == answerId, ct);
        if (previous is not null) return (200, DisputeResult.From(previous.ContentReportId), null);
        var item = answer.SessionItemId is { } id ? await db.SessionItems.SingleOrDefaultAsync(i => i.Id == id, ct) : null;
        var session = item is null ? null : await db.LearningSessions.SingleOrDefaultAsync(s => s.Id == item.SessionId && s.LearnerId == learnerId, ct);
        if (session is null || session.ResultItemId != item!.Id || clock.GetCurrentInstant() >= session.EndsAt || answer.Outcome != "mistake_typo")
            return (409, null, new("answer.dispute_not_allowed", "Оскарження цієї відповіді недоступне."));
        var reportId = await reports.CreateAnswerDisputeAsync(learnerId, answerId, ct)
            ?? throw new InvalidOperationException("Не вдалося створити report відповіді.");
        var word = await db.LearnerWords.SingleAsync(w => w.LearnerId == learnerId && w.EntryId == answer.EntryId, ct);
        DisputeWordState.Restore(word, answer.StateBefore);
        // Перевірка нового слова (звичайна чи extra) відновлює знімок без позначки спроби; планове
        // повторення й додаткова практика виключають слово до кінця доби (ADR-0002).
        if (answer.ExerciseKind is not ("new_word_check" or "extra_new_word_check"))
        {
            var model = JsonNode.Parse(word.ModelState)!.AsObject();
            model["last_scheduled_attempt_day"] = LocalDatePattern.Iso.Format(day!.Day.Date);
            word.ModelState = model.ToJsonString();
        }
        db.AnswerNeutralizations.Add(new AnswerNeutralization { AnswerEventId = answerId, ContentReportId = reportId, CreatedAt = clock.GetCurrentInstant() });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return (200, DisputeResult.From(reportId), null);
    }
}
