namespace InkFlow.Modules.Sources.Domain;

public enum SelectorKind
{
    Css,
    XPath,
    JsonPath,
}

/// <summary>从响应文档中定位内容的声明式选择器。</summary>
public sealed record RuleSelector(SelectorKind Kind, string Expression);

/// <summary>
/// 带强制超时的正则抽取。超时是必填项：正则是 DSL 中唯一可能失控的计算，
/// 无界回溯必须被预算拦截。
/// </summary>
public sealed record RuleRegex(string Pattern, int TimeoutMilliseconds);

/// <summary>对抽取结果施加的纯文本变换，按声明顺序应用。</summary>
public abstract record RuleTransform;

public sealed record TrimTransform : RuleTransform;

/// <summary>字符串替换。<paramref name="From"/> 必须非空；<paramref name="To"/> 允许为空串（即删除）。</summary>
public sealed record ReplaceTransform(string From, string To) : RuleTransform;
