using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ConstantLearning.Features.Learning.Sessions.Start;

namespace ConstantLearning.Features.Learning.Sessions.Answer;

public sealed record AnswerRequest(string? Answer, bool DontKnow)
{
    /// <summary>Хеш наміру команди: порядок JSON-полів неважливий, введений текст зберігається дослівно.</summary>
    public string PayloadHash() => Convert.ToHexStringLower(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(new { action = "answer", answer = Answer, dont_know = DontKnow })));

    public static SessionError? Read(JsonElement root, out AnswerRequest? request)
    {
        request = null;
        if (root.ValueKind != JsonValueKind.Object
            || root.EnumerateObject().Any(property => property.Name is not ("answer" or "dont_know"))
            || root.EnumerateObject().GroupBy(property => property.Name, StringComparer.Ordinal).Any(group => group.Count() > 1)
            || !root.TryGetProperty("dont_know", out var dontKnow)
            || dontKnow.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return Invalid();

        string? answer = null;
        if (root.TryGetProperty("answer", out var value) && value.ValueKind != JsonValueKind.Null)
        {
            if (value.ValueKind != JsonValueKind.String) return Invalid();
            answer = value.GetString();
            if (answer!.Contains('\0') || answer.EnumerateRunes().Count() > 200) return Invalid();
        }
        request = new AnswerRequest(answer, dontKnow.GetBoolean());
        return null;
    }

    public static SessionError Invalid() => new("answer.invalid_request", "Некоректна відповідь на картку.");
}
