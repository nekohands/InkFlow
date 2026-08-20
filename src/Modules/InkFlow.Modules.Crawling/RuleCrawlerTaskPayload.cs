using System.Text.Json;
using System.Text.Json.Serialization;
using InkFlow.Modules.Sources.Rules;

namespace InkFlow.Modules.Crawling;

public sealed record RuleCrawlerTaskPayload(
    Guid SourceId,
    Guid RuleVersionId,
    SourceOperation Operation,
    Guid? SourceBookId,
    Guid? SourceChapterId,
    IReadOnlyDictionary<string, string> Variables)
{
    public const string TaskType = "RuleOperation";

    public string Serialize() => JsonSerializer.Serialize(this, JsonOptions);

    public static RuleCrawlerTaskPayload Deserialize(string json) =>
        JsonSerializer.Deserialize<RuleCrawlerTaskPayload>(json, JsonOptions)
        ?? throw new JsonException("Crawler task payload is empty.");

    private static JsonSerializerOptions JsonOptions { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
