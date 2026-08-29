using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Xml;
using System.Xml.XPath;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Infrastructure;

/// <summary>
/// 统一的声明式选择器求值器。CSS 继续由 AngleSharp 处理，JSONPath 只开放
/// 有界的 root/property/index/wildcard/recursive-property 子集；不支持的语法
/// 统一返回空结果，避免把规则输入当作可执行脚本。
/// </summary>
public sealed class RuleSelectorEvaluator : ISelectorEvaluator
{
    private const int MaxDocumentBytes = 16 * 1024 * 1024;
    private const int MaxMatches = 4_096;
    private const int MaxJsonDepth = 64;
    private const int MaxHtmlPathSteps = 64;
    private const int MaxHtmlDepth = 256;
    private const int MaxHtmlVisitedElements = 100_000;

    private static readonly HtmlParser HtmlParser = new();
    private readonly CssSelectorEvaluator _css = new();

    public string? EvaluateFirst(
        string documentBody,
        RuleSelector selector,
        string? attributeName = null)
    {
        if (selector is null ||
            string.IsNullOrWhiteSpace(documentBody) ||
            string.IsNullOrWhiteSpace(selector.Expression))
        {
            return null;
        }

        return selector.Kind switch
        {
            SelectorKind.Css => _css.EvaluateFirst(documentBody, selector, attributeName),
            SelectorKind.XPath => EvaluateXPathFirst(documentBody, selector.Expression, attributeName),
            SelectorKind.JsonPath => EvaluateJsonFirst(documentBody, selector.Expression, attributeName),
            _ => null,
        };
    }

    public IReadOnlyList<SelectorElementSnapshot> SelectAll(
        string documentBody,
        RuleSelector selector)
    {
        if (selector is null ||
            string.IsNullOrWhiteSpace(documentBody) ||
            string.IsNullOrWhiteSpace(selector.Expression))
        {
            return [];
        }

        return selector.Kind switch
        {
            SelectorKind.Css => _css.SelectAll(documentBody, selector),
            SelectorKind.XPath => SelectXPathAll(documentBody, selector.Expression),
            SelectorKind.JsonPath => SelectJsonAll(documentBody, selector.Expression),
            _ => [],
        };
    }

    private static string? EvaluateXPathFirst(
        string documentBody,
        string expression,
        string? attributeName)
    {
        if (ContainsBlockedDtd(documentBody))
        {
            return null;
        }

        try
        {
            var document = ParseXml(documentBody);
            var iterator = document.CreateNavigator().Select(expression);
            if (iterator.MoveNext() && iterator.Current is { } current)
            {
                if (!string.IsNullOrWhiteSpace(attributeName))
                {
                    return current.NodeType == XPathNodeType.Element
                        ? current.GetAttribute(attributeName.Trim(), string.Empty)
                        : null;
                }

                return current.Value.Trim();
            }
        }
        catch (XmlException)
        {
        }
        catch (XPathException)
        {
        }
        catch (InvalidOperationException)
        {
        }
        catch (ArgumentException)
        {
        }

        return EvaluateHtmlXPathFirst(documentBody, expression, attributeName);
    }

    private static IReadOnlyList<SelectorElementSnapshot> SelectXPathAll(
        string documentBody,
        string expression)
    {
        if (ContainsBlockedDtd(documentBody))
        {
            return [];
        }

        try
        {
            var document = ParseXml(documentBody);
            var iterator = document.CreateNavigator().Select(expression);
            var result = new List<SelectorElementSnapshot>();
            while (iterator.MoveNext())
            {
                if (iterator.Current is not { } current)
                {
                    continue;
                }

                if (result.Count >= MaxMatches)
                {
                    return [];
                }

                result.Add(ToXPathSnapshot(current));
            }

            if (result.Count > 0)
            {
                return result;
            }
        }
        catch (XmlException)
        {
        }
        catch (XPathException)
        {
        }
        catch (InvalidOperationException)
        {
        }
        catch (ArgumentException)
        {
        }

        return SelectHtmlXPathAll(documentBody, expression);
    }

    private static bool ContainsBlockedDtd(string documentBody)
    {
        if (documentBody.IndexOf("<!ENTITY", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        var doctypeStart = documentBody.IndexOf("<!DOCTYPE", StringComparison.OrdinalIgnoreCase);
        if (doctypeStart < 0)
        {
            return false;
        }

        var declarationEnd = documentBody.IndexOf('>', doctypeStart);
        var declaration = declarationEnd >= 0
            ? documentBody[doctypeStart..(declarationEnd + 1)]
            : documentBody[doctypeStart..];
        return declaration.Contains('[', StringComparison.Ordinal) ||
               declaration.Contains("SYSTEM", StringComparison.OrdinalIgnoreCase) ||
               declaration.Contains("PUBLIC", StringComparison.OrdinalIgnoreCase);
    }

    private static string? EvaluateHtmlXPathFirst(
        string documentBody,
        string expression,
        string? attributeName)
    {
        if (!TrySelectHtmlXPath(documentBody, expression, out var plan, out var elements) ||
            plan is null ||
            elements.Count == 0)
        {
            return null;
        }

        var element = elements[0];
        if (!string.IsNullOrWhiteSpace(attributeName))
        {
            return element.GetAttribute(attributeName.Trim());
        }

        if (plan.TerminalAttribute is not null)
        {
            return element.GetAttribute(plan.TerminalAttribute);
        }

        return element.TextContent.Trim();
    }

    private static IReadOnlyList<SelectorElementSnapshot> SelectHtmlXPathAll(
        string documentBody,
        string expression)
    {
        if (!TrySelectHtmlXPath(documentBody, expression, out var plan, out var elements) ||
            plan is null)
        {
            return [];
        }

        if (plan.TerminalAttribute is not null)
        {
            return elements
                .Select(element => new SelectorElementSnapshot(
                    element.GetAttribute(plan.TerminalAttribute) ?? string.Empty,
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)))
                .ToList();
        }

        if (plan.TerminalText)
        {
            return elements
                .Select(element => new SelectorElementSnapshot(
                    element.TextContent.Trim(),
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)))
                .ToList();
        }

        return elements
            .Select(element => new SelectorElementSnapshot(
                element.TextContent.Trim(),
                element.Attributes.ToDictionary(
                    attribute => attribute.Name,
                    attribute => attribute.Value ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase)))
            .ToList();
    }

    private static bool TrySelectHtmlXPath(
        string documentBody,
        string expression,
        out HtmlXPathPlan? plan,
        out IReadOnlyList<IElement> elements)
    {
        plan = null;
        elements = [];
        if (documentBody.Length > MaxDocumentBytes ||
            ContainsBlockedDtd(documentBody) ||
            !TryParseHtmlXPath(expression, out var parsedPlan))
        {
            return false;
        }

        try
        {
            var document = HtmlParser.ParseDocument(documentBody);
            var current = new List<IElement>();
            for (var index = 0; index < parsedPlan.Segments.Count; index++)
            {
                var segment = parsedPlan.Segments[index];
                var candidates = new List<IElement>();
                var added = index == 0
                    ? AddFirstHtmlXPathCandidates(document, segment, candidates)
                    : AddHtmlXPathCandidates(current, segment, candidates);
                if (!added || !TryApplyHtmlPredicates(candidates, segment.Step.Predicates, out current))
                {
                    return false;
                }

                if (current.Count == 0)
                {
                    break;
                }
            }

            plan = parsedPlan;
            elements = current;
            return true;
        }
        catch (DomException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static bool AddFirstHtmlXPathCandidates(
        IDocument document,
        HtmlXPathSegment segment,
        List<IElement> destination)
    {
        if (document.DocumentElement is not { } root)
        {
            return true;
        }

        if (segment.Axis == HtmlXPathAxis.Child)
        {
            if (MatchesHtmlName(root, segment.Step.Name))
            {
                destination.Add(root);
            }

            return true;
        }

        return AddHtmlXPathDescendants(root, segment.Step, destination, includeRoot: true);
    }

    private static bool AddHtmlXPathCandidates(
        IReadOnlyList<IElement> parents,
        HtmlXPathSegment segment,
        List<IElement> destination)
    {
        foreach (var parent in parents)
        {
            if (segment.Axis == HtmlXPathAxis.Child)
            {
                foreach (var child in parent.Children)
                {
                    if (!MatchesHtmlName(child, segment.Step.Name))
                    {
                        continue;
                    }

                    if (destination.Count >= MaxMatches)
                    {
                        return false;
                    }

                    destination.Add(child);
                }
            }
            else if (!AddHtmlXPathDescendants(parent, segment.Step, destination, includeRoot: false))
            {
                return false;
            }
        }

        return true;
    }

    private static bool AddHtmlXPathDescendants(
        IElement root,
        HtmlXPathStep step,
        List<IElement> destination,
        bool includeRoot)
    {
        var pending = new Stack<(IElement Element, int Depth)>();
        pending.Push((root, 0));
        var visited = 0;
        while (pending.Count > 0)
        {
            var (element, depth) = pending.Pop();
            if (++visited > MaxHtmlVisitedElements)
            {
                return false;
            }

            if ((includeRoot || depth > 0) && MatchesHtmlName(element, step.Name))
            {
                if (destination.Count >= MaxMatches)
                {
                    return false;
                }

                destination.Add(element);
            }

            if (depth >= MaxHtmlDepth)
            {
                continue;
            }

            var children = element.Children.ToList();
            for (var index = children.Count - 1; index >= 0; index--)
            {
                pending.Push((children[index], depth + 1));
            }
        }

        return true;
    }

    private static bool TryApplyHtmlPredicates(
        IReadOnlyList<IElement> candidates,
        IReadOnlyList<HtmlXPathPredicate> predicates,
        out List<IElement> result)
    {
        result = [];
        foreach (var candidate in candidates)
        {
            if (predicates
                .Where(predicate => predicate.Kind != HtmlPredicateKind.Position)
                .All(predicate => MatchesHtmlPredicate(candidate, predicate)))
            {
                result.Add(candidate);
            }
        }

        var position = predicates.SingleOrDefault(
            predicate => predicate.Kind == HtmlPredicateKind.Position);
        if (position is not null)
        {
            if (position.Position < 1 || position.Position > result.Count)
            {
                result = [];
            }
            else
            {
                result = [result[position.Position - 1]];
            }
        }

        return result.Count <= MaxMatches;
    }

    private static bool MatchesHtmlPredicate(IElement element, HtmlXPathPredicate predicate) =>
        predicate.Kind switch
        {
            HtmlPredicateKind.HasAttribute => element.HasAttribute(predicate.Name!),
            HtmlPredicateKind.AttributeEquals => string.Equals(
                element.GetAttribute(predicate.Name!),
                predicate.Value,
                StringComparison.Ordinal),
            HtmlPredicateKind.AttributeContains => (element.GetAttribute(predicate.Name!) ?? string.Empty)
                .Contains(predicate.Value!, StringComparison.Ordinal),
            HtmlPredicateKind.AttributeStartsWith => (element.GetAttribute(predicate.Name!) ?? string.Empty)
                .StartsWith(predicate.Value!, StringComparison.Ordinal),
            HtmlPredicateKind.TextEquals => string.Equals(
                element.TextContent.Trim(),
                predicate.Value,
                StringComparison.Ordinal),
            HtmlPredicateKind.TextContains => element.TextContent.Contains(
                predicate.Value!,
                StringComparison.Ordinal),
            HtmlPredicateKind.TextStartsWith => element.TextContent.Trim().StartsWith(
                predicate.Value!,
                StringComparison.Ordinal),
            _ => true,
        };

    private static bool TryParseHtmlXPath(
        string expression,
        out HtmlXPathPlan plan)
    {
        plan = null!;
        if (string.IsNullOrWhiteSpace(expression))
        {
            return false;
        }

        expression = expression.Trim();
        var position = 0;
        HtmlXPathAxis axis;
        if (expression.StartsWith(".//", StringComparison.Ordinal))
        {
            position = 3;
            axis = HtmlXPathAxis.Descendant;
        }
        else if (expression.StartsWith("//", StringComparison.Ordinal))
        {
            position = 2;
            axis = HtmlXPathAxis.Descendant;
        }
        else if (expression.StartsWith("./", StringComparison.Ordinal))
        {
            position = 2;
            axis = HtmlXPathAxis.Child;
        }
        else if (expression.StartsWith("/", StringComparison.Ordinal))
        {
            position = 1;
            axis = HtmlXPathAxis.Child;
        }
        else
        {
            return false;
        }

        var segments = new List<HtmlXPathSegment>();
        string? terminalAttribute = null;
        var terminalText = false;
        while (position < expression.Length)
        {
            if (segments.Count >= MaxHtmlPathSteps ||
                !TryReadHtmlXPathSegment(expression, ref position, out var rawSegment))
            {
                return false;
            }

            rawSegment = rawSegment.Trim();
            if (rawSegment.StartsWith('@'))
            {
                if (segments.Count == 0 ||
                    terminalAttribute is not null ||
                    !TryParseHtmlAttributeName(rawSegment, out terminalAttribute) ||
                    position != expression.Length)
                {
                    return false;
                }

                break;
            }

            if (string.Equals(rawSegment, "text()", StringComparison.Ordinal))
            {
                if (segments.Count == 0 || terminalText || position != expression.Length)
                {
                    return false;
                }

                terminalText = true;
                break;
            }

            if (!TryParseHtmlXPathStep(rawSegment, out var step))
            {
                return false;
            }

            segments.Add(new HtmlXPathSegment(axis, step));
            if (position == expression.Length)
            {
                break;
            }

            if (expression[position] != '/')
            {
                return false;
            }

            position++;
            if (position < expression.Length && expression[position] == '/')
            {
                position++;
                axis = HtmlXPathAxis.Descendant;
            }
            else
            {
                axis = HtmlXPathAxis.Child;
            }
        }

        if (segments.Count == 0)
        {
            return false;
        }

        plan = new HtmlXPathPlan(segments, terminalAttribute, terminalText);
        return true;
    }

    private static bool TryReadHtmlXPathSegment(
        string expression,
        ref int position,
        out string segment)
    {
        var start = position;
        var bracketDepth = 0;
        var quote = '\0';
        while (position < expression.Length)
        {
            var current = expression[position];
            if (quote != '\0')
            {
                if (current == quote && (position == 0 || expression[position - 1] != '\\'))
                {
                    quote = '\0';
                }
            }
            else if (current is '\'' or '"')
            {
                quote = current;
            }
            else if (current == '[')
            {
                bracketDepth++;
            }
            else if (current == ']')
            {
                if (--bracketDepth < 0)
                {
                    segment = string.Empty;
                    return false;
                }
            }
            else if (current == '/' && bracketDepth == 0)
            {
                break;
            }

            position++;
        }

        segment = expression[start..position];
        return quote == '\0' && bracketDepth == 0 && segment.Trim().Length > 0;
    }

    private static bool TryParseHtmlXPathStep(
        string rawStep,
        out HtmlXPathStep step)
    {
        step = null!;
        var position = 0;
        SkipWhitespace(rawStep, ref position);
        var nameStart = position;
        while (position < rawStep.Length && rawStep[position] != '[')
        {
            if (rawStep[position] == ']')
            {
                return false;
            }

            position++;
        }

        var name = rawStep[nameStart..position].Trim();
        if (!IsValidHtmlName(name))
        {
            return false;
        }

        var predicates = new List<HtmlXPathPredicate>();
        while (position < rawStep.Length)
        {
            SkipWhitespace(rawStep, ref position);
            if (position >= rawStep.Length || rawStep[position] != '[')
            {
                return false;
            }

            var end = FindHtmlPredicateEnd(rawStep, position);
            if (end < 0 ||
                !TryParseHtmlPredicate(rawStep[(position + 1)..end], out var predicate))
            {
                return false;
            }

            if (predicate.Kind == HtmlPredicateKind.Position &&
                predicates.Any(existing => existing.Kind == HtmlPredicateKind.Position))
            {
                return false;
            }

            predicates.Add(predicate);
            position = end + 1;
        }

        step = new HtmlXPathStep(name, predicates);
        return true;
    }

    private static int FindHtmlPredicateEnd(string expression, int start)
    {
        var quote = '\0';
        for (var position = start + 1; position < expression.Length; position++)
        {
            var current = expression[position];
            if (quote != '\0')
            {
                if (current == quote && expression[position - 1] != '\\')
                {
                    quote = '\0';
                }
            }
            else if (current is '\'' or '"')
            {
                quote = current;
            }
            else if (current == ']')
            {
                return quote == '\0' ? position : -1;
            }
        }

        return -1;
    }

    private static bool TryParseHtmlPredicate(
        string rawPredicate,
        out HtmlXPathPredicate predicate)
    {
        predicate = null!;
        var expression = rawPredicate.Trim();
        if (int.TryParse(
                expression,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var position))
        {
            predicate = new(HtmlPredicateKind.Position, Position: position);
            return position > 0;
        }

        if (expression.StartsWith('@'))
        {
            var namePosition = 1;
            if (!TryReadHtmlIdentifier(expression, ref namePosition, out var name))
            {
                return false;
            }

            var remainder = expression[namePosition..].Trim();
            if (remainder.Length == 0)
            {
                predicate = new(HtmlPredicateKind.HasAttribute, Name: name);
                return true;
            }

            if (!remainder.StartsWith('=') ||
                !TryReadHtmlLiteral(remainder[1..].Trim(), out var value))
            {
                return false;
            }

            predicate = new(HtmlPredicateKind.AttributeEquals, name, value);
            return true;
        }

        if (expression.StartsWith("text()", StringComparison.Ordinal))
        {
            var remainder = expression["text()".Length..].Trim();
            if (!remainder.StartsWith('=') ||
                !TryReadHtmlLiteral(remainder[1..].Trim(), out var value))
            {
                return false;
            }

            predicate = new(HtmlPredicateKind.TextEquals, Value: value);
            return true;
        }

        foreach (var function in new[] { "contains", "starts-with" })
        {
            if (!expression.StartsWith(function + "(", StringComparison.Ordinal))
            {
                continue;
            }

            if (!TryParseHtmlFunction(expression, function, out var operand, out var literal) ||
                !TryParseHtmlOperand(operand, out var operandKind, out var operandName))
            {
                return false;
            }

            var kind = operandKind == HtmlOperandKind.Text
                ? function == "contains"
                    ? HtmlPredicateKind.TextContains
                    : HtmlPredicateKind.TextStartsWith
                : function == "contains"
                    ? HtmlPredicateKind.AttributeContains
                    : HtmlPredicateKind.AttributeStartsWith;
            predicate = new(kind, operandName, literal);
            return true;
        }

        return false;
    }

    private static bool TryParseHtmlFunction(
        string expression,
        string function,
        out string operand,
        out string literal)
    {
        operand = string.Empty;
        literal = string.Empty;
        var prefix = function + "(";
        if (!expression.StartsWith(prefix, StringComparison.Ordinal) ||
            !expression.EndsWith(')'))
        {
            return false;
        }

        var arguments = expression[prefix.Length..^1];
        var quote = '\0';
        var comma = -1;
        for (var position = 0; position < arguments.Length; position++)
        {
            var current = arguments[position];
            if (quote != '\0')
            {
                if (current == quote && (position == 0 || arguments[position - 1] != '\\'))
                {
                    quote = '\0';
                }
            }
            else if (current is '\'' or '"')
            {
                quote = current;
            }
            else if (current == ',')
            {
                if (comma >= 0)
                {
                    return false;
                }

                comma = position;
            }
        }

        if (quote != '\0' || comma < 0)
        {
            return false;
        }

        operand = arguments[..comma].Trim();
        return TryReadHtmlLiteral(arguments[(comma + 1)..].Trim(), out literal);
    }

    private static bool TryParseHtmlOperand(
        string operand,
        out HtmlOperandKind kind,
        out string? name)
    {
        name = null;
        if (string.Equals(operand, "text()", StringComparison.Ordinal))
        {
            kind = HtmlOperandKind.Text;
            return true;
        }

        var position = 1;
        if (operand.StartsWith('@') &&
            TryReadHtmlIdentifier(operand, ref position, out var parsedName) &&
            position == operand.Length)
        {
            kind = HtmlOperandKind.Attribute;
            name = parsedName;
            return true;
        }

        kind = default;
        return false;
    }

    private static bool TryParseHtmlAttributeName(string expression, out string? name)
    {
        name = null;
        var position = 1;
        if (!TryReadHtmlIdentifier(expression, ref position, out var parsedName) ||
            position != expression.Length)
        {
            return false;
        }

        name = parsedName;
        return true;
    }

    private static bool TryReadHtmlLiteral(string expression, out string value)
    {
        value = string.Empty;
        if (expression.Length < 2 || expression[0] is not ('\'' or '"'))
        {
            return false;
        }

        var quote = expression[0];
        if (expression[^1] != quote)
        {
            return false;
        }

        for (var position = 1; position < expression.Length - 1; position++)
        {
            if (expression[position] == quote && expression[position - 1] != '\\')
            {
                return false;
            }
        }

        value = expression[1..^1]
            .Replace("\\'", "'", StringComparison.Ordinal)
            .Replace("\\\"", "\"", StringComparison.Ordinal)
            .Replace("\\\\", "\\", StringComparison.Ordinal);
        return !value.Any(char.IsControl);
    }

    private static bool TryReadHtmlIdentifier(
        string expression,
        ref int position,
        out string name)
    {
        var start = position;
        while (position < expression.Length && IsHtmlNameCharacter(expression[position]))
        {
            position++;
        }

        name = expression[start..position];
        return IsValidHtmlName(name);
    }

    private static bool IsValidHtmlName(string name)
    {
        if (name == "*")
        {
            return true;
        }

        if (name.Length == 0 ||
            (!char.IsLetter(name[0]) && name[0] != '_'))
        {
            return false;
        }

        return name.Skip(1).All(IsHtmlNameCharacter);
    }

    private static bool IsHtmlNameCharacter(char value) =>
        char.IsLetterOrDigit(value) || value is '_' or '-' or ':' or '.';

    private static bool MatchesHtmlName(IElement element, string name) =>
        name == "*" ||
        string.Equals(element.LocalName, name, StringComparison.OrdinalIgnoreCase);

    private static void SkipWhitespace(string expression, ref int position)
    {
        while (position < expression.Length && char.IsWhiteSpace(expression[position]))
        {
            position++;
        }
    }

    private static XPathDocument ParseXml(string documentBody)
    {
        if (documentBody.Length > MaxDocumentBytes)
        {
            throw new XmlException("selector document exceeds the size limit.");
        }

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            MaxCharactersInDocument = MaxDocumentBytes,
            MaxCharactersFromEntities = 0,
            XmlResolver = null,
        };
        using var reader = XmlReader.Create(new StringReader(documentBody), settings);
        return new XPathDocument(reader);
    }

    private static SelectorElementSnapshot ToXPathSnapshot(XPathNavigator current)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var attributeCursor = current.Clone();
        if (attributeCursor.NodeType == XPathNodeType.Element &&
            attributeCursor.MoveToFirstAttribute())
        {
            do
            {
                attributes[attributeCursor.Name] = attributeCursor.Value;
            }
            while (attributeCursor.MoveToNextAttribute());
        }

        return new(current.Value.Trim(), attributes);
    }

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
            if (result.Count >= MaxMatches)
            {
                return [];
            }

            result.Add(ToSnapshot(element));
        }

        return result;
    }

    private static JsonDocument? ParseJson(string documentBody)
    {
        if (documentBody.Length > MaxDocumentBytes)
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
                    MaxDepth = MaxJsonDepth,
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
        if (destination.Count >= MaxMatches)
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

    private enum HtmlXPathAxis
    {
        Child,
        Descendant,
    }

    private enum HtmlPredicateKind
    {
        HasAttribute,
        AttributeEquals,
        AttributeContains,
        AttributeStartsWith,
        TextEquals,
        TextContains,
        TextStartsWith,
        Position,
    }

    private enum HtmlOperandKind
    {
        Attribute,
        Text,
    }

    private sealed record HtmlXPathPlan(
        IReadOnlyList<HtmlXPathSegment> Segments,
        string? TerminalAttribute,
        bool TerminalText);

    private sealed record HtmlXPathSegment(
        HtmlXPathAxis Axis,
        HtmlXPathStep Step);

    private sealed record HtmlXPathStep(
        string Name,
        IReadOnlyList<HtmlXPathPredicate> Predicates);

    private sealed record HtmlXPathPredicate(
        HtmlPredicateKind Kind,
        string? Name = null,
        string? Value = null,
        int Position = 0);

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
