using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ConstantLearning.Features.Content.Reports;

internal sealed record ContentReportRequest(string Origin, Guid? EntryId, string? Field,
    string? ProposedValue, string? Comment, string? SearchQuery)
{
    public static ContentReportError? Read(JsonElement root, out ContentReportRequest? request)
    {
        request = null;
        if (root.ValueKind != JsonValueKind.Object
            || root.EnumerateObject().Any(property => property.Name is not
                ("origin" or "entry_id" or "field" or "proposed_value" or "comment" or "search_query"))
            || root.EnumerateObject().GroupBy(property => property.Name, StringComparer.Ordinal).Any(group => group.Count() > 1)
            || !root.TryGetProperty("origin", out var origin) || origin.ValueKind != JsonValueKind.String
            || origin.GetString() is not ("card" or "missing_word"))
        {
            return Invalid();
        }

        Guid? entryId = null;
        if (root.TryGetProperty("entry_id", out var id) && id.ValueKind != JsonValueKind.Null)
        {
            if (id.ValueKind != JsonValueKind.String || !Guid.TryParseExact(id.GetString(), "D", out var parsed))
            {
                return Invalid();
            }

            entryId = parsed;
        }

        if (!TryText(root, "field", 32, out var field) || field is not (null or "lemma" or "translation" or "pronunciation" or "example")
            || !TryText(root, "proposed_value", 500, out var proposed)
            || !TryText(root, "comment", 1000, out var comment)
            || !TryText(root, "search_query", 200, out var query))
        {
            return Invalid();
        }

        if (origin.GetString() == "card" && (entryId is null || query is not null)
            || origin.GetString() == "missing_word" && (entryId is not null || string.IsNullOrWhiteSpace(query)))
        {
            return new ContentReportError("report.entry_or_query_required", "Вкажіть слово або текст пошуку.");
        }

        if (origin.GetString() == "card" && field is null || origin.GetString() == "missing_word" && field is not null)
        {
            return Invalid();
        }

        request = new ContentReportRequest(origin.GetString()!, entryId, field,
            string.IsNullOrWhiteSpace(proposed) ? null : proposed, comment, query);
        return null;
    }

    private static bool TryText(JsonElement root, string name, int maxLength, out string? text)
    {
        text = null;
        if (!root.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        text = value.GetString();
        return !text!.Contains('\0') && text.EnumerateRunes().Count() <= maxLength;
    }

    public static ContentReportError Invalid() => new("report.invalid_request", "Некоректне повідомлення про неточність.");
}

public sealed record ContentReportError(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message);

public sealed record ContentReportAccepted(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("message")] string Message = "Дякуємо, передали на перевірку");
