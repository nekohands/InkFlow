namespace InkFlow.Modules.Sources.Rules;

public sealed record CompiledSourceRequest(
    string Method,
    Uri Uri,
    IReadOnlyDictionary<string, string> Headers,
    IReadOnlyDictionary<string, string> Form);

public sealed class SourceRequestCompiler
{
    public CompiledSourceRequest Compile(
        SourceRuleDocument document,
        SourceOperation operation,
        IReadOnlyDictionary<string, string> variables)
    {
        var rule = document.GetOperation(operation)
            ?? throw new InvalidOperationException($"Source operation {operation} is not configured.");

        var baseUri = new Uri(document.BaseUrl, UriKind.Absolute);
        var expandedUrl = RuleTemplate.Expand(rule.Request.Url, variables);
        var uri = Uri.TryCreate(expandedUrl, UriKind.Absolute, out var absolute)
            ? absolute
            : new Uri(baseUri, expandedUrl);

        var query = ExpandMap(rule.Request.Query, variables);
        if (query.Count > 0)
        {
            var builder = new UriBuilder(uri);
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(builder.Query))
            {
                parts.Add(builder.Query.TrimStart('?'));
            }

            parts.AddRange(query.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
            builder.Query = string.Join('&', parts);
            uri = builder.Uri;
        }

        return new CompiledSourceRequest(
            rule.Request.Method.ToUpperInvariant(),
            uri,
            ExpandMap(rule.Request.Headers, variables),
            ExpandMap(rule.Request.Form, variables));
    }

    private static IReadOnlyDictionary<string, string> ExpandMap(
        IReadOnlyDictionary<string, string>? source,
        IReadOnlyDictionary<string, string> variables)
    {
        if (source is null || source.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        return source.ToDictionary(
            pair => pair.Key,
            pair => RuleTemplate.Expand(pair.Value, variables),
            StringComparer.OrdinalIgnoreCase);
    }
}
