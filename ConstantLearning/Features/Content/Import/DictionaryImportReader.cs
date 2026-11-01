using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic.FileIO;

namespace ConstantLearning.Features.Content.Import;

public sealed record DictionaryImportRow(IReadOnlyDictionary<string, string> Fields, JsonElement? Evidence,
    string ContentHash)
{
    public string this[string field] => Fields[field];
    public string? Optional(string field) => this[field].Length == 0 ? null : this[field];
    public int? Rank(string field) => Optional(field) is { } value
        ? int.Parse(value, CultureInfo.InvariantCulture) : null;
    public string[] Values(string field) => this[field].Split(';', StringSplitOptions.TrimEntries
                                                                 | StringSplitOptions.RemoveEmptyEntries);
}

public sealed class DictionaryImportReader
{
    public static IReadOnlyList<string> Columns { get; } = Array.AsReadOnly(new[]
    {
        "id", "lemma_pl", "part_of_speech", "gender", "gender_pair_id", "aspect_pair_id", "translation_uk",
        "accepted_answers_pl", "pronunciation_uk", "domain", "legalization_stage", "topic", "frequency_rank",
        "utility_rank", "show_context_before", "example_pl", "example_uk", "validation_status",
        "validation_sources", "reviewed_by", "review_note"
    });

    public async Task<IReadOnlyList<DictionaryImportRow>> ReadAsync(string directory,
        CancellationToken cancellationToken = default)
    {
        var csv = await File.ReadAllBytesAsync(Path.Combine(directory, "course.csv"), cancellationToken);
        var proofs = await File.ReadAllBytesAsync(Path.Combine(directory, "evidence.jsonl"), cancellationToken);
        using var readiness = JsonDocument.Parse(await File.ReadAllBytesAsync(
            Path.Combine(directory, "readiness.json"), cancellationToken));
        var manifest = readiness.RootElement;
        Require(manifest.GetProperty("ready").GetBoolean()
                && manifest.GetProperty("approval_policy_confirmed").GetBoolean(), "Набір не готовий до імпорту.");
        Require(manifest.GetProperty("course_sha256").GetString() == Hash(csv)
                && manifest.GetProperty("evidence_sha256").GetString() == Hash(proofs),
            "Контрольна сума набору не відповідає readiness.json.");

        var evidence = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        using (var lines = new StringReader(Encoding.UTF8.GetString(proofs)))
        {
            while (await lines.ReadLineAsync(cancellationToken) is { } line)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                using var document = JsonDocument.Parse(line);
                var proof = document.RootElement.Clone();
                Require(evidence.TryAdd(proof.GetProperty("id").GetString()!, proof), "Повтор ID доказу.");
            }
        }

        using var stream = new MemoryStream(csv);
        using var parser = new TextFieldParser(stream, new UTF8Encoding(false, true), true)
        {
            TextFieldType = FieldType.Delimited, HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = false
        };
        parser.SetDelimiters(",");
        Require(parser.ReadFields()?.SequenceEqual(Columns) == true, "Невідповідні колонки редакторського CSV.");
        var result = new List<DictionaryImportRow>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var lemmas = new HashSet<string>(StringComparer.Ordinal);
        while (!parser.EndOfData)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fields = parser.ReadFields()!;
            Require(fields.Length == Columns.Count, "Невідповідна кількість полів CSV.");
            var values = Columns.Zip(fields).ToDictionary(pair => pair.First,
                pair => pair.Second.Replace("\r\n", "\n", StringComparison.Ordinal).Normalize().Trim(),
                StringComparer.Ordinal);
            foreach (var name in new[] { "translation_uk", "accepted_answers_pl" })
                values[name] = string.Join(';', values[name].Split(';', StringSplitOptions.TrimEntries));
            evidence.TryGetValue(values["validation_sources"], out var proof);
            var row = new DictionaryImportRow(values, proof.ValueKind == JsonValueKind.Undefined ? null : proof, "");
            Validate(row);
            Require(ids.Add(row["id"]) && lemmas.Add(Normalize(row["lemma_pl"])), "Повтор ID або леми в CSV.");
            result.Add(row with { ContentHash = ContentHash(row) });
        }
        Require(result.Count == manifest.GetProperty("released").GetInt32(), "Кількість записів не відповідає readiness.json.");
        return result.AsReadOnly();
    }

    private static void Validate(DictionaryImportRow row)
    {
        foreach (var (field, maximum) in new Dictionary<string, int>
        {
            ["id"] = 64, ["lemma_pl"] = 200, ["part_of_speech"] = 32, ["gender"] = 8,
            ["gender_pair_id"] = 64, ["aspect_pair_id"] = 64, ["pronunciation_uk"] = 200,
            ["example_pl"] = 500, ["example_uk"] = 500, ["legalization_stage"] = 32,
            ["topic"] = 32, ["review_note"] = 500, ["reviewed_by"] = 200
        }) Require(row[field].Length <= maximum, $"Завелике поле {field}.");
        Require(row["id"].Length > 0 && row["lemma_pl"].Length > 0, "Порожній ID або лема.");
        Require(row["validation_status"] is "draft" or "needs_review" or "approved", "Невідомий статус перевірки.");
        Require(row["domain"] is "everyday" or "legalization", "Невідомий домен.");
        Require(row["show_context_before"] is "true" or "false", "Очікується true або false.");
        foreach (var field in new[] { "frequency_rank", "utility_rank" })
            Require(row[field].Length == 0 || int.TryParse(row[field], NumberStyles.None, CultureInfo.InvariantCulture,
                out var rank) && rank > 0, "Ранг має бути додатним цілим числом.");
        foreach (var field in new[] { "translation_uk", "accepted_answers_pl" })
        {
            var variants = row.Values(field);
            Require(variants.Length <= short.MaxValue && variants.All(value => value.Length <= 200)
                    && variants.Distinct(StringComparer.Ordinal).Count() == variants.Length,
                "Завеликі або повторені варіанти відповіді.");
        }
        if (row["validation_status"] != "approved") return;

        Require(Regex.IsMatch(row["lemma_pl"], "^[a-ząćęłńóśźż]+$", RegexOptions.CultureInvariant),
            "Схвалена лема має бути одним польським словом.");
        Require(row["part_of_speech"] is "noun" or "verb" or "adj" or "adv" or "pron" or "det"
            or "prep" or "conj" or "particle" or "num" or "intj", "Невідома частина мови.");
        Require(row["part_of_speech"] != "noun" || row["gender"] is "m" or "f" or "n" or "pl",
            "Для іменника потрібен рід або позначка множини.");
        foreach (var field in new[] { "translation_uk", "accepted_answers_pl", "pronunciation_uk", "example_pl",
                     "example_uk", "topic", "reviewed_by" })
            Require(row[field].Length > 0, $"Схвалений запис не має поля {field}.");
        Require(row.Rank(row["domain"] == "everyday" ? "frequency_rank" : "utility_rank") is not null,
            "Для схваленого запису потрібен ранг домену.");
        Require(row["domain"] != "legalization" || row["legalization_stage"] is "appointment" or "application"
            or "supplement" or "status" or "decision" or "pickup", "Невідомий етап легалізації.");
        Require(row.Evidence.HasValue, "Схвалений запис не має доказу.");
        var proof = row.Evidence!.Value;
        Require(Text(proof, "lemma_pl") == row["lemma_pl"] && Text(proof, "part_of_speech") == row["part_of_speech"],
            "Доказ не відповідає лемі та частині мови.");
        Require(Text(proof, "source_url") == "https://pl.wiktionary.org/wiki/" + Uri.EscapeDataString(row["lemma_pl"]),
            "Невідповідна сторінка джерела.");
        Require(Text(proof, "sense_index").Length is > 0 and <= 32 && Text(proof, "ipa").Length is > 0 and <= 200
                && Regex.IsMatch(Text(proof, "source_record_sha256"), "^[0-9a-f]{64}$")
                && Regex.IsMatch(Text(proof, "usage_example_sha256"), "^[0-9a-f]{64}$"), "Неповний доказ джерела.");
        Require(row["gender"] != "pl" || Strings(proof, "source_tags").Contains("plural"),
            "Множину не підтверджено джерелом.");
        var candidates = Strings(proof, "translation_candidates").ToHashSet(StringComparer.Ordinal);
        if (proof.TryGetProperty("translation_correction", out var corrections))
            foreach (var correction in corrections.EnumerateArray())
            {
                Require(candidates.Contains(Text(correction, "original"))
                        && Uri.TryCreate(Text(correction, "source_url"), UriKind.Absolute, out var url)
                        && url.Scheme == "https", "Виправлення перекладу не має джерела.");
                candidates.Add(Text(correction, "corrected"));
            }
        var translations = candidates.Select(Normalize).ToHashSet(StringComparer.Ordinal);
        Require(row.Values("translation_uk").All(value => translations.Contains(Normalize(value))),
            "Переклад не підтверджено вибраним значенням джерела.");
        var answers = Strings(proof, "accepted_answers_pl").Append(row["lemma_pl"]).ToHashSet(StringComparer.Ordinal);
        Require(row.Values("accepted_answers_pl").Contains(row["lemma_pl"])
                && row.Values("accepted_answers_pl").All(answers.Contains), "Польський варіант не підтверджено доказом.");
        Require(Hash(Encoding.UTF8.GetBytes(row["example_pl"])) != Text(proof, "usage_example_sha256"),
            "Приклад джерела скопійовано дослівно.");
        Require(!Regex.IsMatch(row["example_pl"], "[а-яіїєґА-ЯІЇЄҐ]")
                && Regex.IsMatch(row["example_uk"], "[а-яіїєґА-ЯІЇЄҐ]")
                && Regex.IsMatch(row["translation_uk"], "[а-яіїєґА-ЯІЇЄҐ]")
                && !Regex.IsMatch(row["pronunciation_uk"], "[a-zA-Z]"), "Невідповідна мова поля.");
        Require(Text(proof, "example_origin") == "original/LLM-editorial"
                && row["reviewed_by"].Contains("LLM", StringComparison.Ordinal)
                && Text(proof, "reviewer_batch").Length is > 0 and <= 200,
            "Не вказано погоджену LLM-редактуру та походження прикладу.");
    }

    internal static string Text(JsonElement value, string property) => value.TryGetProperty(property, out var item)
        && item.ValueKind == JsonValueKind.String ? item.GetString()! : "";
    private static IEnumerable<string> Strings(JsonElement value, string property) => value.TryGetProperty(property, out var item)
        ? item.EnumerateArray().Select(element => element.GetString()!) : [];
    private static string Normalize(string value) => value.Normalize().Replace("\u0301", "", StringComparison.Ordinal).Trim().ToLowerInvariant();
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }

    private static string ContentHash(DictionaryImportRow row)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var field in Columns) writer.WriteString(field, row[field]);
            writer.WritePropertyName("evidence");
            if (row.Evidence is { } proof) WriteCanonical(writer, proof);
            else writer.WriteNullValue();
            writer.WriteEndObject();
        }
        return Hash(stream.ToArray());
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var property in value.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
            {
                writer.WritePropertyName(property.Name);
                WriteCanonical(writer, property.Value);
            }
            writer.WriteEndObject();
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var item in value.EnumerateArray()) WriteCanonical(writer, item);
            writer.WriteEndArray();
        }
        else value.WriteTo(writer);
    }
}
