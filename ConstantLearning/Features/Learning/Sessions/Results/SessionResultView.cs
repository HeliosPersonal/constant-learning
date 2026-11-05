using System.Text.Json;
using System.Text.Json.Serialization;
using ConstantLearning.Features.Learning.Data;
using ConstantLearning.Features.Learning.Disputes;
using ConstantLearning.Features.Learning.Sessions.Answer;
using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace ConstantLearning.Features.Learning.Sessions.Results;

public sealed record LastResultResponse(
    [property: JsonPropertyName("item_id")] Guid ItemId,
    [property: JsonPropertyName("answer")] AnswerResponse Answer,
    [property: JsonPropertyName("dispute")] DisputeResult? Dispute);

public static class SessionResultView
{
    // Відкрита session шукається в межах однієї мови: частковий UNIQUE активної session тепер per-language
    // (T09), тож learner може мати відкриту session у кількох мовах одночасно без їхнього змішування (AC-04).
    public static Task<LearningSession?> FindOpenAsync(AppDbContext db, Guid learnerId, string targetLanguage, Instant now, CancellationToken ct) =>
        db.LearningSessions.SingleOrDefaultAsync(s => s.LearnerId == learnerId && s.TargetLanguage == targetLanguage
            && s.ResultItemId != null && s.EndsAt > now, ct);

    public static async Task<LastResultResponse?> ReadAsync(AppDbContext db, LearningSession session, CancellationToken ct)
    {
        if (session.ResultItemId is not { } itemId) return null;
        var item = await db.SessionItems.SingleAsync(i => i.Id == itemId && i.SessionId == session.Id, ct);
        var answer = JsonSerializer.Deserialize<AnswerResponse>(item.ResponsePayload!)
            ?? throw new InvalidOperationException("Збережений результат відповіді порожній.");
        var reportId = await db.AnswerNeutralizations.Where(n => n.AnswerEventId == answer.AnswerEventId)
            .Select(n => (Guid?)n.ContentReportId).SingleOrDefaultAsync(ct);
        return new(itemId, answer, reportId is { } id ? DisputeResult.From(id) : null);
    }
}
