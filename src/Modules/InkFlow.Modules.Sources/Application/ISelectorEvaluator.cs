using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

/// <summary>
/// 选择器求值抽象（CSS/XPath/JSONPath）。具体引擎随后续工作包以适配器注入；
/// 本接口让 RuleAdapter 与解析器实现解耦，并可完全离线测试。
/// </summary>
public interface ISelectorEvaluator
{
    /// <summary>返回文档中第一个匹配值；无匹配返回 null。</summary>
    string? EvaluateFirst(string documentBody, RuleSelector selector);
}
