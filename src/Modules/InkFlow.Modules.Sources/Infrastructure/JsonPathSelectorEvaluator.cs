using System.Globalization;
using System.Text.Json;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Infrastructure;

/// <summary>Bounded JSONPath evaluator for the supported rule-language subset.</summary>
internal sealed class JsonPathSelectorEvaluator : ISelectorLanguageEvaluator
{
    public SelectorKind Kind => SelectorKind.JsonPath;

    public string? EvaluateFirst(
        string documentBody,
        RuleSelector selector,
        string? attributeName = null) =>
        EvaluateJsonFirst(documentBody, selector.Expression, attributeName);

    public IReadOnlyList<SelectorElementSnapshot> SelectAll(
        string documentBody,
        RuleSelector selector) =>
        SelectJsonAll(documentBody, selector.Expression);

    private static string? EvaluateJsonFirst(
        string documentBody,
        string expression,
        string? attributeName)
    {
        if (!string.IsNullOrWhiteSpace(attributeName))
        {
            return null;
        }

        using var document = ParseJson(documentBody);
        if (document is null)
        {
            return null;
        }

        var steps = JsonPathParser.Parse(expression);
        if (steps is null)
        {
            return null;
        }

        foreach (var element in Evaluate(document.RootElement, steps))
        {
            var value = ScalarText(element);
            if (value is not null)
            {
                return value;
            }
        }

        return null;
    }

    private static IReadOnlyList<SelectorElementSnapshot> SelectJsonAll(
        string documentBody,
        string expression)
    {
        using var document = ParseJson(documentBody);
        if (document is null)
        {
            return [];
        }

        var steps = JsonPathParser.Parse(expression);
        if (steps is null)
        {
            return [];
        }

        var result = new List<SelectorElementSnapshot>();
        foreach (var element in Evaluate(document.RootElement, steps))
        {
            if (result.Count >= SelectorEvaluationLimits.MaxMatches)
            {
                return [];
            }

            result.Add(ToSnapshot(element));
        }

        return result;
    }

    private static JsonDocument? ParseJson(string documentBody)
    {
        if (documentBody.Length > SelectorEvaluationLimits.MaxDocumentBytes)
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(
                documentBody,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = SelectorEvaluationLimits.MaxJsonDepth,
                });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IEnumerable<JsonElement> Evaluate(
        JsonElement root,
        IReadOnlyList<JsonPathStep> steps)
    {
        IReadOnlyList<JsonElement> current = [root];
        foreach (var step in steps)
        {
            var next = new List<JsonElement>();
            foreach (var element in current)
            {
                if (!AddStepMatches(element, step, next))
                {
                    return [];
                }
            }

            current = next;
            if (current.Count == 0)
            {
                break;
            }
        }

        return current;
    }

    private static bool AddStepMatches(
        JsonElement element,
        JsonPathStep step,
        List<JsonElement> destination)
    {
        switch (step.Kind)
        {
            case JsonPathStepKind.Property:
                if (element.ValueKind == JsonValueKind.Object &&
                    element.TryGetProperty(step.Name!, out var property))
                {
                    return TryAddJsonMatch(destination, property);
                }

                return true;
            case JsonPathStepKind.Index:
                if (element.ValueKind == JsonValueKind.Array &&
                    step.Index >= 0 &&
                    step.Index < element.GetArrayLength())
                {
                    return TryAddJsonMatch(destination, element[step.Index]);
                }

                return true;
            case JsonPathStepKind.Wildcard:
                return AddChildren(element, destination);
            case JsonPathStepKind.RecursiveProperty:
                return AddRecursivePropertyMatches(element, step.Name!, destination);
        }

        return false;
    }

    private static bool TryAddJsonMatch(List<JsonElement> destination, JsonElement value)
    {
            if (destination.Count >= SelectorEvaluationLimits.MaxMatches)
        {
            return false;
        }

        destination.Add(value);
        return true;
    }

    private static bool AddChildren(JsonElement element, List<JsonElement> destination)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (!TryAddJsonMatch(destination, property.Value))
                {
                    return false;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
            {
                if (!TryAddJsonMatch(destination, child))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool AddRecursivePropertyMatches(
        JsonElement element,
        string propertyName,
        List<JsonElement> destination)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (string.Equals(property.Name, propertyName, StringComparison.Ordinal))
                    {
                        if (!TryAddJsonMatch(destination, property.Value))
                        {
                            return false;
                        }
                    }

                    if (!AddRecursivePropertyMatches(property.Value, propertyName, destination))
                    {
                        return false;
                    }
                }

                return true;
            case JsonValueKind.Array:
                foreach (var child in element.EnumerateArray())
                {
                    if (!AddRecursivePropertyMatches(child, propertyName, destination))
                    {
                        return false;
                    }
                }

                return true;
        }

        return true;
    }

    private static SelectorElementSnapshot ToSnapshot(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return new(ScalarText(element) ?? string.Empty, new Dictionary<string, string>());
        }

        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? text = null;
        foreach (var property in element.EnumerateObject())
        {
            var value = ScalarText(property.Value);
            if (value is null)
            {
                continue;
            }

            attributes[property.Name] = value;
            if (text is null &&
                (string.Equals(property.Name, "title", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(property.Name, "name", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(property.Name, "text", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(property.Name, "value", StringComparison.OrdinalIgnoreCase)))
            {
                text = value;
            }
        }

        return new(text ?? string.Empty, attributes);
    }

    private static string? ScalarText(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => bool.TrueString.ToLowerInvariant(),
            JsonValueKind.False => bool.FalseString.ToLowerInvariant(),
            _ => null,
        };

    private enum JsonPathStepKind
    {
        Property,
        Index,
        Wildcard,
        RecursiveProperty,
    }

    private sealed record JsonPathStep(JsonPathStepKind Kind, string? Name = null, int Index = -1);

    private static class JsonPathParser
    {
        public static IReadOnlyList<JsonPathStep>? Parse(string? expression)
        {
            if (string.IsNullOrWhiteSpace(expression) || expression[0] != '$')
            {
                return null;
            }

            var steps = new List<JsonPathStep>();
            var position = 1;
            while (position < expression.Length)
            {
                if (steps.Count >= 64)
                {
                    return null;
                }

                if (expression[position] == '.')
                {
                    var recursive = position + 1 < expression.Length && expression[position + 1] == '.';
                    position += recursive ? 2 : 1;
                    if (position >= expression.Length)
                    {
                        return null;
                    }

                    if (expression[position] == '*')
                    {
                        if (recursive)
                        {
                            return null;
                        }

                        steps.Add(new JsonPathStep(JsonPathStepKind.Wildcard));
                        position++;
                        continue;
                    }

                    if (!TryReadName(expression, ref position, out var name))
                    {
                        return null;
                    }

                    steps.Add(new(
                        recursive ? JsonPathStepKind.RecursiveProperty : JsonPathStepKind.Property,
                        name));
                    continue;
                }

                if (expression[position] != '[' ||
                    !TryReadBracket(expression, ref position, out var bracket))
                {
                    return null;
                }

                if (bracket == "*")
                {
                    steps.Add(new JsonPathStep(JsonPathStepKind.Wildcard));
                }
                else if (int.TryParse(
                             bracket,
                             NumberStyles.None,
                             CultureInfo.InvariantCulture,
                             out var index) && index >= 0)
                {
                    steps.Add(new(JsonPathStepKind.Index, Index: index));
                }
                else if (TryUnquote(bracket, out var name))
                {
                    steps.Add(new(JsonPathStepKind.Property, name));
                }
                else
                {
                    return null;
                }
            }

            return steps;
        }

        private static bool TryReadName(
            string expression,
            ref int position,
            out string name)
        {
            var start = position;
            while (position < expression.Length &&
                   (char.IsLetterOrDigit(expression[position]) ||
                    expression[position] is '_' or '-'))
            {
                position++;
            }

            name = expression[start..position];
            return name.Length > 0 &&
                   (char.IsLetter(name[0]) || name[0] is '_' or '-');
        }

        private static bool TryReadBracket(
            string expression,
            ref int position,
            out string content)
        {
            var start = ++position;
            var quote = '\0';
            for (; position < expression.Length; position++)
            {
                var current = expression[position];
                if (quote != '\0')
                {
                    if (current == quote && expression[position - 1] != '\\')
                    {
                        quote = '\0';
                    }

                    continue;
                }

                if (current is '\'' or '"')
                {
                    quote = current;
                }
                else if (current == ']')
                {
                    content = expression[start..position].Trim();
                    position++;
                    return quote == '\0' && content.Length > 0;
                }
            }

            content = string.Empty;
            return false;
        }

        private static bool TryUnquote(string value, out string name)
        {
            name = string.Empty;
            if (value.Length < 2 ||
                value[0] is not ('\'' or '"') ||
                value[^1] != value[0])
            {
                return false;
            }

            var inner = value[1..^1];
            if (value[0] == '\'' && inner.Contains('"'))
            {
                // Single-quoted JSONPath names are not JSON strings; keep the
                // v1 grammar deliberately literal and reject ambiguous escapes.
                return false;
            }

            name = inner.Replace("\\'", "'", StringComparison.Ordinal)
                .Replace("\\\"", "\"", StringComparison.Ordinal)
                .Replace("\\\\", "\\", StringComparison.Ordinal);
            return name.Length > 0 && !name.Any(char.IsControl);
        }
    }
}
