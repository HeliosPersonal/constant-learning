using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ConstantLearning.Features.Content.Editor.Queue;

public sealed record EditorError(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message);
public sealed record EditorQueueItem(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("reporter_alias")] string ReporterAlias,
    [property: JsonPropertyName("origin")] string Origin,
    [property: JsonPropertyName("entry_id")] Guid? EntryId,
    [property: JsonPropertyName("lemma_pl")] string? LemmaPl,
    [property: JsonPropertyName("field")] string? Field,
    [property: JsonPropertyName("current_value")] string? CurrentValue,
    [property: JsonPropertyName("proposed_value")] string? ProposedValue,
    [property: JsonPropertyName("comment")] string? Comment,
    [property: JsonPropertyName("search_query")] string? SearchQuery,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt);
public sealed record EditorQueuePage(
    [property: JsonPropertyName("items")] IReadOnlyList<EditorQueueItem> Items,
    [property: JsonPropertyName("has_next")] bool HasNext,
    [property: JsonPropertyName("has_prev")] bool HasPrev,
    [property: JsonPropertyName("next_cursor")] Guid? NextCursor);
public sealed record ContentReportClosed(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("outcome")] string Outcome,
    [property: JsonPropertyName("rejection_reason")] string? RejectionReason,
    [property: JsonPropertyName("resulting_version_id")] Guid? ResultingVersionId,
    [property: JsonPropertyName("closed_at")] DateTimeOffset ClosedAt);

public sealed record EditorQueueRequest(Guid? After, Guid? Before, int Limit)
{
    public static EditorError? Read(IQueryCollection query, out EditorQueueRequest? request)
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

    public static EditorError Invalid() => new("editor.invalid_request", "Перевірте параметри черги або рішення.");
}

public sealed record EditorDecision(string Decision, string? RejectionReason, string? CorrectedValue)
{
    public static EditorError? Read(JsonElement json, out EditorDecision? request)
    {
        request = null;
        if (json.ValueKind != JsonValueKind.Object) return EditorQueueRequest.Invalid();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in json.EnumerateObject())
            if (!seen.Add(property.Name) || property.Name is not ("decision" or "rejection_reason" or "corrected_value")
                || property.Value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null)) return EditorQueueRequest.Invalid();
        string? ReadText(string name) => json.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;
        var decision = ReadText("decision");
        var reason = ReadText("rejection_reason");
        var corrected = ReadText("corrected_value");
        if (decision is not ("approve" or "reject" or "add_to_candidates")) return EditorQueueRequest.Invalid();
        if (decision == "reject" && reason is null)
            return new("editor.rejection_reason_required", "Вкажіть причину відхилення.");
        if (reason is not null && reason is not ("source_not_confirming" or "already_correct" or "out_of_scope"
                or "duplicate" or "already_in_dictionary" or "phrase_not_lemma")
            || decision != "reject" && reason is not null || decision == "reject" && corrected is not null
            || corrected is not null && (string.IsNullOrWhiteSpace(corrected) || corrected.EnumerateRunes().Count() > 500
                || corrected.Any(char.IsControl))) return EditorQueueRequest.Invalid();
        request = new(decision, reason, corrected?.Trim().Normalize());
        return null;
    }
}

internal sealed record EditorDecisionResult(int Status, ContentReportClosed? Report = null, EditorError? Error = null);
