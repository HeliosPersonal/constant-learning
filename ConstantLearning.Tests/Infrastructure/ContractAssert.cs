using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Json.Schema;
using Xunit.Sdk;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace ConstantLearning.Tests.Infrastructure;

public static class ContractAssert
{
    private static readonly Lazy<IReadOnlyList<JsonNode>> OpenApiDocuments = new(ReadOpenApiDocuments);
    private static readonly Lazy<IReadOnlyDictionary<string, JsonObject>> EventSchemas = new(ReadEventSchemas);

    public static async Task ResponseAsync(string operationId, HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Response(operationId, (int)response.StatusCode, string.IsNullOrWhiteSpace(body) ? null : body,
            response.Content.Headers.ContentType?.MediaType);
    }

    public static void Response(string operationId, int statusCode, string? json, string? mediaType = "application/json")
    {
        var (document, operation) = FindOperation(operationId);
        var response = operation["responses"]?[statusCode.ToString(CultureInfo.InvariantCulture)]
                       ?? throw new XunitException($"Статус {statusCode} операції {operationId} не описано в OpenAPI.");
        response = Resolve(document, response);
        if (response["content"] is not JsonObject content)
        {
            if (json is not null)
            {
                throw new XunitException($"Відповідь {operationId}/{statusCode} повинна бути без тіла.");
            }

            return;
        }

        if (mediaType is null || content[mediaType]?["schema"] is not JsonNode schema || json is null)
        {
            throw new XunitException($"Відсутнє тіло або неочікуваний Content-Type для {operationId}/{statusCode}.");
        }

        var root = JsonNode.Parse(schema.ToJsonString().Replace("#/components/schemas/", "#/$defs/", StringComparison.Ordinal))!.AsObject();
        root["$schema"] = "https://json-schema.org/draft/2020-12/schema";
        root["$defs"] = JsonNode.Parse(document["components"]!["schemas"]!.ToJsonString()
            .Replace("#/components/schemas/", "#/$defs/", StringComparison.Ordinal));
        Validate(root, json, $"OpenAPI {operationId}/{statusCode}");
    }

    public static void EventRow(string channel, string json)
    {
        if (!EventSchemas.Value.TryGetValue(channel, out var schema))
        {
            throw new XunitException($"Канал {channel} не описано в events.md.");
        }

        Validate(schema, json, $"events.md {channel}");
    }

    private static void Validate(JsonObject schema, string json, string context)
    {
        using var document = JsonDocument.Parse(json);
        var result = JsonSchema.FromText(schema.ToJsonString()).Evaluate(document.RootElement,
            new EvaluationOptions { RequireFormatValidation = true, OutputFormat = OutputFormat.List });
        if (!result.IsValid)
        {
            throw new XunitException($"Порушено контракт {context}: {JsonSerializer.Serialize(result)}");
        }
    }

    private static JsonNode Resolve(JsonNode document, JsonNode node)
    {
        if (node["$ref"] is not JsonValue reference)
        {
            return node;
        }

        var path = reference.GetValue<string>();
        if (!path.StartsWith("#/", StringComparison.Ordinal))
        {
            throw new XunitException($"Зовнішнє посилання {path} не підтримується тестовим контрактом.");
        }

        return path[2..].Split('/').Aggregate(document, (current, segment) =>
            current[segment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal)]
            ?? throw new XunitException($"Не знайдено посилання {path}."));
    }

    // Операція може жити в будь-якому з кількох контрактів фіч (кожен Contracts/**/openapi.yaml);
    // шукаємо її за operationId по всіх документах і повертаємо документ-власник для резолву $ref.
    private static (JsonNode Document, JsonObject Operation) FindOperation(string operationId)
    {
        foreach (var document in OpenApiDocuments.Value)
        {
            var operation = document["paths"]!.AsObject().SelectMany(path => path.Value!.AsObject())
                .Select(method => method.Value)
                .SingleOrDefault(value => value is JsonObject && value["operationId"]?.GetValue<string>() == operationId);
            if (operation is JsonObject match)
            {
                return (document, match);
            }
        }

        throw new XunitException($"Операцію {operationId} не описано в OpenAPI.");
    }

    private static IReadOnlyList<JsonNode> ReadOpenApiDocuments()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Contracts");
        return Directory.EnumerateFiles(directory, "openapi.yaml", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(ReadOpenApi)
            .ToArray();
    }

    private static JsonNode ReadOpenApi(string path)
    {
        using var reader = File.OpenText(path);
        var yaml = new YamlStream();
        yaml.Load(reader);
        return ToJson(yaml.Documents.Single().RootNode)!;
    }

    private static JsonNode? ToJson(YamlNode node) => node switch
    {
        YamlMappingNode mapping => new JsonObject(mapping.Children.Select(pair =>
            KeyValuePair.Create(((YamlScalarNode)pair.Key).Value!, ToJson(pair.Value)))),
        YamlSequenceNode sequence => new JsonArray(sequence.Children.Select(ToJson).ToArray()),
        YamlScalarNode scalar => ScalarToJson(scalar),
        _ => throw new XunitException("Непідтримуваний вузол YAML у контракті.")
    };

    private static JsonNode? ScalarToJson(YamlScalarNode scalar)
    {
        var value = scalar.Value;
        if (scalar.Style == ScalarStyle.Plain)
        {
            if (value is null or "null" or "~")
            {
                return null;
            }

            if (bool.TryParse(value, out var boolean))
            {
                return JsonValue.Create(boolean);
            }

            if (decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            {
                return JsonValue.Create(number);
            }
        }

        return JsonValue.Create(value);
    }

    private static IReadOnlyDictionary<string, JsonObject> ReadEventSchemas()
    {
        var markdown = File.ReadAllText(ContractPath("events.md"));
        var schemas = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        var channels = Regex.Matches(markdown, @"^## Channel: `([^`]+)`\r?$", RegexOptions.Multiline);
        for (var index = 0; index < channels.Count; index++)
        {
            var start = channels[index].Index + channels[index].Length;
            var end = index + 1 < channels.Count ? channels[index + 1].Index : markdown.Length;
            var properties = new JsonObject();
            var required = new JsonArray();
            foreach (Match field in Regex.Matches(markdown[start..end], @"^\| `([^`]+)` \| ([^|]+) \|", RegexOptions.Multiline))
            {
                var name = field.Groups[1].Value;
                properties[name] = EventFieldSchema(field.Groups[2].Value.Trim());
                required.Add(name);
            }

            if (properties.Count == 0)
            {
                throw new XunitException("У каналі events.md не знайдено полів payload.");
            }

            schemas.Add(channels[index].Groups[1].Value, new JsonObject
            {
                ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
                ["type"] = "object",
                ["required"] = required,
                ["properties"] = properties
            });
        }

        return schemas;
    }

    private static JsonObject EventFieldSchema(string type)
    {
        var schema = type switch
        {
            "UUID" => new JsonObject { ["type"] = "string", ["format"] = "uuid" },
            "UUID або null" => new JsonObject { ["type"] = new JsonArray("string", "null"), ["format"] = "uuid" },
            "integer" => new JsonObject { ["type"] = "integer" },
            "bigint" => new JsonObject { ["type"] = "integer", ["minimum"] = long.MinValue, ["maximum"] = long.MaxValue },
            "date" => new JsonObject { ["type"] = "string", ["format"] = "date" },
            "timestamptz" => new JsonObject { ["type"] = "string", ["format"] = "date-time" },
            _ when Regex.IsMatch(type, @"^`[^`]+`( / `[^`]+`)+$") => new JsonObject
            {
                ["type"] = "string",
                ["enum"] = new JsonArray(Regex.Matches(type, @"`([^`]+)`")
                    .Select(match => (JsonNode?)JsonValue.Create(match.Groups[1].Value)).ToArray())
            },
            _ => throw new XunitException($"Непідтримуваний тип {type} у events.md; оновіть перевірку контракту.")
        };
        return schema;
    }

    private static string ContractPath(string file) => Path.Combine(AppContext.BaseDirectory, "Contracts", file);
}
