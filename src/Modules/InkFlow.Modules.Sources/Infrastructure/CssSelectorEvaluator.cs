using AngleSharp.Html.Parser;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Infrastructure;

/// <summary>
/// CSS 选择器求值器(AngleSharp HtmlParser 实现)。
/// XPath / JSONPath v1 暂未支持:返回 null(视为无匹配),由规则校验与执行器按缺失处理;
/// 两个引擎随后续工作包以同一接口接入。
/// </summary>
public sealed class CssSelectorEvaluator : ISelectorEvaluator
{
    private static readonly HtmlParser Parser = new();

    public string? EvaluateFirst(string documentBody, RuleSelector selector)
    {
        if (selector.Kind != SelectorKind.Css || string.IsNullOrWhiteSpace(documentBody))
        {
            return null;
        }

        var document = Parser.ParseDocument(documentBody);
        return document.QuerySelector(selector.Expression)?.TextContent.Trim();
    }
}
