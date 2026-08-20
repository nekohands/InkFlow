using System.Text.Json;
using System.Text.Json.Serialization;

namespace InkFlow.Modules.Sources.Rules;

[Flags]
public enum SourceCapability
{
    None = 0,
    Search = 1 << 0,
    BookInfo = 1 << 1,
    Toc = 1 << 2,
    Content = 1 << 3,
    Update = 1 << 4,
    Login = 1 << 5,
    BrowserRequired = 1 << 6,
    Images = 1 << 7
}

public enum SourceOperation
{
    Search,
    BookInfo,
    Toc,
    Content,
    Update
}

public enum SelectorKind
{
    Css,
    XPath,
    JsonPath,
    Regex
}

public enum TransformKind
{
    Trim,
    Replace,
    RegexReplace,
    RegexCapture,
    CollapseWhitespace,
    HtmlDecode
}

public sealed record RuleExecutionBudget(
    int MaxRequests = 8,
    long MaxBytes = 5 * 1024 * 1024,
    int MaxRedirects = 5,
    int MaxDepth = 8,
    int MaxExecutionTimeMs = 10_000,
    int MaxRegexTimeMs = 250,
    int MaxResultSize = 5_000);

public sealed record RequestRule(
    string Method,
    string Url,
    IReadOnlyDictionary<string, string>? Headers = null,
    IReadOnlyDictionary<string, string>? Query = null,
    IReadOnlyDictionary<string, string>? Form = null);

public sealed record TransformRule(
    TransformKind Kind,
    string? Argument = null,
    string? Replacement = null);

public sealed record ExtractionFieldRule(
    SelectorKind Kind,
    string Expression,
    string? Attribute = null,
    IReadOnlyList<TransformRule>? Transforms = null);

public sealed record SourceOperationRule(
    RequestRule Request,
    IReadOnlyDictionary<string, ExtractionFieldRule> Fields,
    bool Multiple = false);

public sealed record SourceRuleDocument(
    int SchemaVersion,
    string Name,
    string BaseUrl,
    SourceCapability Capabilities,
    RuleExecutionBudget Budget,
    SourceOperationRule? Search = null,
    SourceOperationRule? BookInfo = null,
    SourceOperationRule? Toc = null,
    SourceOperationRule? Content = null,
    SourceOperationRule? Update = null)
{
    public SourceOperationRule? GetOperation(SourceOperation operation) => operation switch
    {
        SourceOperation.Search => Search,
        SourceOperation.BookInfo => BookInfo,
        SourceOperation.Toc => Toc,
        SourceOperation.Content => Content,
        SourceOperation.Update => Update,
        _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
    };
}

public static class SourceRuleJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static SourceRuleDocument Deserialize(string json) =>
        JsonSerializer.Deserialize<SourceRuleDocument>(json, Options)
        ?? throw new JsonException("Source rule JSON did not contain a rule document.");

    public static string Serialize(SourceRuleDocument rule) =>
        JsonSerializer.Serialize(rule, Options);

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
