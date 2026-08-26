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
/// 列表抽取绑定(Toc/Search 等多结果能力):选择重复条目集合,
/// 从条目的指定属性(通常 href)剥离前后缀得到外部 ID,标题取条目文本。
/// </summary>
public sealed record RuleListBinding(
    string ItemsSelector,
    string ExternalIdAttribute,
    string IdPrefixToStrip,
    string IdSuffixToStrip);
