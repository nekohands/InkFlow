using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using HtmlAgilityPack;
using Json.Path;

namespace InkFlow.Modules.Sources.Rules;

public sealed record RuleExtractionResult(IReadOnlyList<IReadOnlyDictionary<string, string>> Rows);

public sealed class RuleDocumentExtractor
{
    public RuleExtractionResult Extract(
        SourceOperationRule operation,
        string content,
        RuleExecutionBudget budget)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(content);

        var fields = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (fieldName, fieldRule) in operation.Fields)
        {
            fields[fieldName] = ExtractField(content, fieldRule, budget);
        }

        var rowCount = operation.Multiple
            ? fields.Values.Select(values => values.Count).DefaultIfEmpty(0).Max()
            : 1;

        if (rowCount > budget.MaxResultSize)
        {
            throw new InvalidOperationException($"Rule returned {rowCount} rows, exceeding MaxResultSize {budget.MaxResultSize}.");
        }

        var rows = new List<IReadOnlyDictionary<string, string>>(rowCount);
        for (var index = 0; index < rowCount; index++)
        {
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (fieldName, values) in fields)
            {
                if (values.Count == 0)
                {
                    continue;
                }

                if (index < values.Count)
                {
                    row[fieldName] = values[index];
                }
                else if (values.Count == 1)
                {
                    row[fieldName] = values[0];
                }
            }

            rows.Add(row);
        }

        return new RuleExtractionResult(rows);
    }

    private static IReadOnlyList<string> ExtractField(
        string content,
        ExtractionFieldRule field,
        RuleExecutionBudget budget)
    {
        IReadOnlyList<string> values = field.Kind switch
        {
            SelectorKind.Css => ExtractCss(content, field),
            SelectorKind.XPath => ExtractXPath(content, field),
            SelectorKind.JsonPath => ExtractJsonPath(content, field),
            SelectorKind.Regex => ExtractRegex(content, field.Expression, budget.MaxRegexTimeMs),
            _ => throw new ArgumentOutOfRangeException(nameof(field.Kind), field.Kind, null)
        };

        if (field.Transforms is null || field.Transforms.Count == 0)
        {
            return values;
        }

        return values
            .Select(value => ApplyTransforms(value, field.Transforms, budget.MaxRegexTimeMs))
            .ToArray();
    }

    private static IReadOnlyList<string> ExtractCss(string content, ExtractionFieldRule field)
    {
        var document = new HtmlParser().ParseDocument(content);
        return document.QuerySelectorAll(field.Expression)
            .Select(element => ExtractElementValue(element.TextContent, element.InnerHtml, attribute => element.GetAttribute(attribute), field.Attribute))
            .Where(value => value is not null)
            .Cast<string>()
            .ToArray();
    }

    private static IReadOnlyList<string> ExtractXPath(string content, ExtractionFieldRule field)
    {
        var document = new HtmlDocument();
        document.LoadHtml(content);
        var nodes = document.DocumentNode.SelectNodes(field.Expression);
        if (nodes is null)
        {
            return Array.Empty<string>();
        }

        return nodes
            .Select(node => ExtractElementValue(node.InnerText, node.InnerHtml, attribute => node.Attributes[attribute]?.Value, field.Attribute))
            .Where(value => value is not null)
            .Cast<string>()
            .ToArray();
    }

    private static IReadOnlyList<string> ExtractJsonPath(string content, ExtractionFieldRule field)
    {
        var node = JsonNode.Parse(content)
            ?? throw new InvalidOperationException("JSON response is empty.");
        var path = JsonPath.Parse(field.Expression);
        var result = path.Evaluate(node);

        return result.Matches
            .Select(match => match.Value)
            .Where(value => value is not null)
            .Select(value => value!.GetValueKind() == System.Text.Json.JsonValueKind.String
                ? value.GetValue<string>()
                : value.ToJsonString())
            .ToArray();
    }

    private static IReadOnlyList<string> ExtractRegex(string content, string pattern, int timeoutMs)
    {
        var regex = new Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(timeoutMs));
        return regex.Matches(content)
            .Select(match => match.Groups.Count > 1 ? match.Groups[1].Value : match.Value)
            .ToArray();
    }

    private static string? ExtractElementValue(
        string text,
        string html,
        Func<string, string?> getAttribute,
        string? attribute) => attribute?.ToLowerInvariant() switch
        {
            null or "text" => text,
            "html" => html,
            _ => getAttribute(attribute)
        };

    private static string ApplyTransforms(string value, IReadOnlyList<TransformRule> transforms, int regexTimeoutMs)
    {
        var current = value;
        foreach (var transform in transforms)
        {
            current = transform.Kind switch
            {
                TransformKind.Trim => current.Trim(),
                TransformKind.Replace => current.Replace(transform.Argument ?? string.Empty, transform.Replacement ?? string.Empty, StringComparison.Ordinal),
                TransformKind.RegexReplace => Regex.Replace(
                    current,
                    transform.Argument ?? throw new InvalidOperationException("RegexReplace requires a pattern."),
                    transform.Replacement ?? string.Empty,
                    RegexOptions.CultureInvariant | RegexOptions.NonBacktracking,
                    TimeSpan.FromMilliseconds(regexTimeoutMs)),
                TransformKind.RegexCapture => CaptureRegex(current, transform.Argument, transform.Replacement, regexTimeoutMs),
                TransformKind.CollapseWhitespace => CollapseWhitespace(current, regexTimeoutMs),
                TransformKind.HtmlDecode => WebUtility.HtmlDecode(current),
                _ => throw new ArgumentOutOfRangeException(nameof(transform.Kind), transform.Kind, null)
            };
        }

        return current;
    }

    private static string CaptureRegex(string value, string? pattern, string? group, int timeoutMs)
    {
        var regex = new Regex(
            pattern ?? throw new InvalidOperationException("RegexCapture requires a pattern."),
            RegexOptions.CultureInvariant | RegexOptions.NonBacktracking,
            TimeSpan.FromMilliseconds(timeoutMs));
        var match = regex.Match(value);
        if (!match.Success)
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(group))
        {
            return match.Groups.Count > 1 ? match.Groups[1].Value : match.Value;
        }

        return int.TryParse(group, out var index)
            ? match.Groups[index].Value
            : match.Groups[group].Value;
    }

    private static string CollapseWhitespace(string value, int timeoutMs) =>
        Regex.Replace(
            value,
            @"\s+",
            " ",
            RegexOptions.CultureInvariant | RegexOptions.NonBacktracking,
            TimeSpan.FromMilliseconds(timeoutMs)).Trim();
}
