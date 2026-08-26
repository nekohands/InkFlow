using AngleSharp.Html.Parser;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Infrastructure;

/// <summary>
/// CSS 选择器求值器(AngleSharp HtmlParser 实现)。
/// XPath / JSONPath v1 暂未支持:EvaluateFirst 返回 null、SelectAll 返回空,
/// 由规则校验与执行器按缺失处理;两个引擎随后续工作包以同一接口接入。
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

        var element = Parser.ParseDocument(documentBody).QuerySelector(selector.Expression);
        if (element is null)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(attributeName)
            ? element.TextContent.Trim()
            : element.GetAttribute(attributeName.Trim());
    }

    public IReadOnlyList<SelectorElementSnapshot> SelectAll(
        string documentBody, RuleSelector selector)
    {
        if (selector.Kind != SelectorKind.Css || string.IsNullOrWhiteSpace(documentBody))
        {
            return [];
        }

        return Parser.ParseDocument(documentBody)
            .QuerySelectorAll(selector.Expression)
            .Select(e => new SelectorElementSnapshot(
                e.TextContent.Trim(),
                e.Attributes.ToDictionary(a => a.Name, a => a.Value ?? string.Empty, StringComparer.OrdinalIgnoreCase)))
            .ToList();
    }
}
