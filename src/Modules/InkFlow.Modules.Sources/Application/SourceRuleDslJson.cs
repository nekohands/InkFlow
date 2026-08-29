using System.Text.Json;
using System.Text.Json.Serialization;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

/// <summary>
/// Versioned JSON boundary for the Source Rule DSL.
/// The wire shape is strict and uses camel-case property names/string enum values;
/// persisted legacy enum numbers remain readable, while all new documents are emitted canonically.
/// </summary>
public sealed record SourceRuleDslParseResult(
    bool IsSuccess,
    SourceRuleDsl? Document,
    IReadOnlyList<string> Errors);

public static class SourceRuleDslJson
{
    public const string SchemaVersion = SourceRuleDslValidator.SupportedSchemaVersion;
    public const int MaxJsonLength = 256 * 1024;

    public static SourceRuleDslParseResult Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Failure("ruleDsl: JSON document must not be empty.");
        }

        if (json.Length > MaxJsonLength)
        {
            return Failure("ruleDsl: JSON document exceeds the source-rule-dsl-v1 size limit.");
        }

        try
        {
            var document = JsonSerializer.Deserialize<SourceRuleDsl>(json, CreateOptions());
            if (document is null)
            {
                return Failure("ruleDsl: JSON document must not be null.");
            }

            var violations = SourceRuleDslValidator.Validate(document);
            return violations.Count == 0
                ? new SourceRuleDslParseResult(true, document, [])
                : new SourceRuleDslParseResult(false, null, violations);
        }
        catch (JsonException)
        {
            // Do not echo the input or converter details: a rule document may contain
            // credential references or site-specific data that must not enter errors.
            return Failure("ruleDsl: JSON does not match the source-rule-dsl-v1 contract.");
        }
    }

    public static string Serialize(SourceRuleDsl dsl)
    {
        ArgumentNullException.ThrowIfNull(dsl);

        var violations = SourceRuleDslValidator.Validate(dsl);
        if (violations.Count > 0)
        {
            throw new InvalidOperationException(
                $"rule DSL cannot be serialized: {string.Join(" | ", violations)}");
        }

        var json = JsonSerializer.Serialize(dsl, CreateOptions(writeIndented: true));
        if (json.Length > MaxJsonLength)
        {
            throw new InvalidOperationException(
                "rule DSL cannot be serialized: JSON document exceeds the source-rule-dsl-v1 size limit.");
        }

        return json;
    }

    private static JsonSerializerOptions CreateOptions(bool writeIndented = false)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = writeIndented,
            PropertyNameCaseInsensitive = false,
            RespectRequiredConstructorParameters = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };

        options.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: true));
        options.Converters.Add(new RuleTransformJsonConverter());
        return options;
    }

    private static SourceRuleDslParseResult Failure(string error) =>
        new(false, null, [error]);

    private sealed class RuleTransformJsonConverter : JsonConverter<RuleTransform>
    {
        public override RuleTransform Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("rule transform must be an object");
            }

            var properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var property in root.EnumerateObject())
            {
                if (!properties.TryAdd(property.Name, property.Value))
                {
                    throw new JsonException("rule transform contains duplicate properties");
                }
            }

            var kind = RequiredString(properties, "kind");
            return kind switch
            {
                "trim" when HasExactly(properties, "kind") => new TrimTransform(),
                "replace" when HasExactly(properties, "kind", "from", "to") =>
                    new ReplaceTransform(
                        RequiredString(properties, "from"),
                        RequiredString(properties, "to")),
                "trim" or "replace" => throw new JsonException(
                    $"rule transform '{kind}' has an invalid property set"),
                _ => throw new JsonException("rule transform kind is not supported"),
            };
        }

        public override void Write(
            Utf8JsonWriter writer,
            RuleTransform value,
            JsonSerializerOptions options)
        {
            switch (value)
            {
                case TrimTransform:
                    writer.WriteStartObject();
                    writer.WriteString("kind", "trim");
                    writer.WriteEndObject();
                    return;
                case ReplaceTransform replace:
                    writer.WriteStartObject();
                    writer.WriteString("kind", "replace");
                    writer.WriteString("from", replace.From);
                    writer.WriteString("to", replace.To);
                    writer.WriteEndObject();
                    return;
                default:
                    throw new JsonException("rule transform type is not supported");
            }
        }

        private static string RequiredString(
            IReadOnlyDictionary<string, JsonElement> properties,
            string name)
        {
            if (!properties.TryGetValue(name, out var value) ||
                value.ValueKind != JsonValueKind.String ||
                value.GetString() is not { } text)
            {
                throw new JsonException($"rule transform property '{name}' must be a string");
            }

            return text;
        }

        private static bool HasExactly(
            IReadOnlyDictionary<string, JsonElement> properties,
            params string[] names) =>
            properties.Count == names.Length && names.All(properties.ContainsKey);
    }
}
