using System.Globalization;
using System.Text.Json.Serialization;
using ConstantLearning.Features.Content.Editor.Queue;

namespace ConstantLearning.Features.Content.Editor.Rollback;

public sealed record AppliedChange(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("entry_id")] Guid EntryId,
    [property: JsonPropertyName("lemma_pl")] string LemmaPl,
    [property: JsonPropertyName("field")] string Field,
    [property: JsonPropertyName("previous_value")] string? PreviousValue,
    [property: JsonPropertyName("new_value")] string NewValue,
    [property: JsonPropertyName("confirmed_by")] string ConfirmedBy,
    [property: JsonPropertyName("content_report_id")] Guid ContentReportId,
    [property: JsonPropertyName("applied_at")] DateTimeOffset AppliedAt,
    [property: JsonPropertyName("rollback_allowed")] bool RollbackAllowed);

public sealed record AppliedChangePage(
    [property: JsonPropertyName("items")] IReadOnlyList<AppliedChange> Items,
    [property: JsonPropertyName("has_next")] bool HasNext,
    [property: JsonPropertyName("has_prev")] bool HasPrev,
    [property: JsonPropertyName("next_cursor")] Guid? NextCursor);

public sealed record RollbackResult(
    [property: JsonPropertyName("change_id")] Guid ChangeId,
    [property: JsonPropertyName("entry_id")] Guid EntryId,
    [property: JsonPropertyName("published_version_id")] Guid PublishedVersionId,
    [property: JsonPropertyName("rolled_back_at")] DateTimeOffset RolledBackAt);

public sealed record AppliedChangesRequest(Guid? After, Guid? Before, int Limit)
{
    public static EditorError? Read(IQueryCollection query, out AppliedChangesRequest? request)
    {
        request = null;
        if (query.Any(pair => pair.Value.Count != 1 || pair.Key is not ("after" or "before" or "limit"))) return Invalid();
        Guid? after = null, before = null;
        if (query.ContainsKey("after"))
        {
            if (!Guid.TryParseExact(query["after"], "D", out var id)) return Invalid();
            after = id;
        }
        if (query.ContainsKey("before"))
        {
            if (!Guid.TryParseExact(query["before"], "D", out var id)) return Invalid();
            before = id;
        }
        if (after is not null && before is not null) return Invalid();
        var limit = 20;
        if (query.ContainsKey("limit") && (!int.TryParse(query["limit"], NumberStyles.None,
                CultureInfo.InvariantCulture, out limit) || limit is < 1 or > 100)) return Invalid();
        request = new(after, before, limit);
        return null;
    }

    public static EditorError Invalid() => new("editor.invalid_request", "Перевірте параметри списку застосованих змін.");
}

internal sealed record RollbackResponse(int Status, RollbackResult? Result = null, EditorError? Error = null);
