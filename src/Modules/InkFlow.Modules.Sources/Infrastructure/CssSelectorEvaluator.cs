using AngleSharp.Html.Parser;
using AngleSharp.Dom;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Infrastructure;

/// <summary>
/// CSS 选择器求值器(AngleSharp HtmlParser 实现)。
/// 多选择器场景由 <see cref="RuleSelectorEvaluator"/> 统一分派；此类保留为
/// CSS-only 的兼容实现。
/// </summary>
public sealed class CssSelectorEvaluator : ISelectorEvaluator
{
    private static readonly HtmlParser Parser = new();

    public string? EvaluateFirst(
        string documentBody, RuleSelector selector, string? attributeName = null)
    {
        if (selector.Kind != SelectorKind.Css || string.IsNullOrWhiteSpace(documentBody))
        {
            return null;
        }

        try
        {
            var element = Parser.ParseDocument(documentBody).QuerySelector(selector.Expression);
            if (element is null)
            {
                return null;
            }

            return string.IsNullOrWhiteSpace(attributeName)
                ? element.TextContent.Trim()
                : element.GetAttribute(attributeName.Trim());
        }
        catch (DomException)
        {
            return null;
        }
    }

    public IReadOnlyList<SelectorElementSnapshot> SelectAll(
        string documentBody, RuleSelector selector)
    {
        if (selector.Kind != SelectorKind.Css || string.IsNullOrWhiteSpace(documentBody))
        {
            return [];
        }

        try
        {
            return Parser.ParseDocument(documentBody)
                .QuerySelectorAll(selector.Expression)
                .Select(e => new SelectorElementSnapshot(
                    e.TextContent.Trim(),
                    e.Attributes.ToDictionary(a => a.Name, a => a.Value ?? string.Empty, StringComparer.OrdinalIgnoreCase)))
                .ToList();
        }
        catch (DomException)
        {
            return [];
        }
    }
}
