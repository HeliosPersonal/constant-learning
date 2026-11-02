using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ConstantLearning.Features.Content.Import;

namespace ConstantLearning.Tests.Content.Import;

internal sealed class DictionaryImportTestBundle : IDisposable
{
    public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "dictionary-import-" + Guid.NewGuid().ToString("N"));
    public Dictionary<string, string> Row { get; } = DictionaryImportReader.Columns.ToDictionary(column => column, _ => "");
    public JsonObject Evidence { get; } = new()
    {
        ["id"] = "proof-zaba", ["lemma_pl"] = "żaba", ["part_of_speech"] = "noun",
        ["source_url"] = "https://pl.wiktionary.org/wiki/%C5%BCaba", ["source_record_sha256"] = new string('a', 64),
        ["sense_index"] = "1.1", ["ipa"] = "ʐaba", ["translation_candidates"] = new JsonArray("жаба", "жабка"),
        ["source_tags"] = new JsonArray(), ["usage_example_sha256"] = new string('b', 64),
        ["reviewer_batch"] = "test-editorial.jsonl", ["example_origin"] = "original/LLM-editorial"
    };

    public DictionaryImportTestBundle()
    {
        Row["id"] = "editorial-zaba-not-a-uuid";
        Row["lemma_pl"] = "żaba";
        Row["part_of_speech"] = "noun";
        Row["gender"] = "f";
        Row["translation_uk"] = "жаба;жабка";
        Row["accepted_answers_pl"] = "żaba";
        Row["pronunciation_uk"] = "жаба";
        Row["domain"] = "everyday";
        Row["topic"] = "animals";
        Row["frequency_rank"] = "10";
        Row["show_context_before"] = "true";
        Row["example_pl"] = "To jest zielona żaba.";
        Row["example_uk"] = "Це зелена жаба.";
        Row["validation_status"] = "approved";
        Row["validation_sources"] = "proof-zaba";
        Row["reviewed_by"] = "LLM-editorial/source-backed";
    }

    public async Task WriteAsync(bool quoteAll = false, bool reverseEvidence = false)
    {
        Directory.CreateDirectory(DirectoryPath);
        string Quote(string value) => quoteAll || value.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? '"' + value.Replace("\"", "\"\"", StringComparison.Ordinal) + '"' : value;
        var csv = string.Join(',', DictionaryImportReader.Columns) + "\r\n"
                  + string.Join(',', DictionaryImportReader.Columns.Select(column => Quote(Row[column]))) + "\r\n";
        var proof = reverseEvidence
            ? new JsonObject(Evidence.Reverse().Select(pair => KeyValuePair.Create(pair.Key, pair.Value?.DeepClone())))
            : Evidence;
        var evidence = proof.ToJsonString() + "\n";
        await File.WriteAllTextAsync(Path.Combine(DirectoryPath, "course.csv"), csv, new UTF8Encoding(false));
        await File.WriteAllTextAsync(Path.Combine(DirectoryPath, "evidence.jsonl"), evidence, new UTF8Encoding(false));
        await File.WriteAllTextAsync(Path.Combine(DirectoryPath, "readiness.json"), JsonSerializer.Serialize(new
        {
            ready = true, approval_policy_confirmed = true, released = 1, target = 1,
            course_sha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(csv))),
            evidence_sha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(evidence)))
        }));
    }

    public void Dispose()
    {
        var resolved = Path.GetFullPath(DirectoryPath);
        if (resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)
            && Path.GetFileName(resolved).StartsWith("dictionary-import-", StringComparison.Ordinal)
            && Directory.Exists(resolved)) Directory.Delete(resolved, true);
    }
}
