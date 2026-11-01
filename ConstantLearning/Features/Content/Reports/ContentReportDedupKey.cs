using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ConstantLearning.Features.Content.Reports;

internal static class ContentReportDedupKey
{
    public static string Create(ContentReportRequest request)
    {
        string[] fields = request.Origin == "missing_word"
            ? [request.Origin, Normalize(request.SearchQuery)]
            : [request.Origin, request.EntryId!.Value.ToString("D"), Normalize(request.Field),
                Normalize(request.ProposedValue), Normalize(request.Comment)];
        // JSON-масив зберігає межі полів: текст із роздільниками не породжує колізій конкатенації.
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(fields))));
    }

    private static string Normalize(string? value) => string.Join(' ', (value ?? "").Normalize(NormalizationForm.FormC)
        .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant().Normalize(NormalizationForm.FormC);
}
