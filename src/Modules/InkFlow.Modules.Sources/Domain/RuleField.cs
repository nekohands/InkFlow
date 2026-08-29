namespace InkFlow.Modules.Sources.Domain;

/// <summary>
/// 单个输出字段的抽取定义。抽取来源必须是选择器或正则二者之一，
/// 抽取结果再依次经过 <see cref="Transforms"/> 变换。
/// <paramref name="Attribute"/> 非空时从命中元素的该属性取值(如 meta content/href),否则取文本。
/// </summary>
public sealed record RuleField(
    string Name,
    RuleSelector? Selector,
    RuleRegex? Regex,
    IReadOnlyList<RuleTransform> Transforms,
    string? Attribute = null);

/// <summary>
/// 从当前响应中派生、仅供同一次受控分页续页请求使用的临时变量。
/// 与 <see cref="RuleField"/> 使用相同的抽取/变换形状，但不会成为能力结果字段，
/// 也不会跨 RuleAdapter 执行持久化。
/// </summary>
public sealed record RuleResponseVariable(
    string Name,
    RuleSelector? Selector,
    RuleRegex? Regex,
    IReadOnlyList<RuleTransform> Transforms,
    string? Attribute = null);

/// <summary>
/// 列表抽取绑定(Toc/Search 等多结果能力):选择重复条目集合,
/// 从条目的指定属性(通常 href)剥离前后缀得到外部 ID,标题取条目文本。
/// <paramref name="ItemsSelectorKind"/> 默认为 CSS；JSONPath 条目可用
/// <paramref name="TextAttribute"/> 指定标题属性。
/// </summary>
public sealed record RuleListBinding(
    string ItemsSelector,
    string ExternalIdAttribute,
    string IdPrefixToStrip,
    string IdSuffixToStrip,
    SelectorKind ItemsSelectorKind = SelectorKind.Css,
    string? TextAttribute = null);
