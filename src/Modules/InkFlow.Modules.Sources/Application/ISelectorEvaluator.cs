using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

/// <summary>命中元素的快照:文本内容与全部属性,供字段/列表抽取使用。</summary>
public sealed record SelectorElementSnapshot(
    string TextContent,
    IReadOnlyDictionary<string, string> Attributes);

/// <summary>
/// 选择器求值抽象(CSS/XPath/JSONPath)。默认实现由 `RuleSelectorEvaluator` 统一分派；本接口让
/// RuleAdapter 与解析器实现解耦，并可完全离线测试。
/// </summary>
public interface ISelectorEvaluator
{
    /// <summary>返回文档中第一个匹配值;无匹配返回 null。attributeName 非空时取元素属性。</summary>
    string? EvaluateFirst(string documentBody, RuleSelector selector, string? attributeName = null);

    /// <summary>
    /// 返回与选择器匹配的全部元素快照(列表抽取用)。
    /// XPath 与 JSONPath 仅开放受控子集；不支持的语法返回空结果。
    /// </summary>
    IReadOnlyList<SelectorElementSnapshot> SelectAll(string documentBody, RuleSelector selector);
}
