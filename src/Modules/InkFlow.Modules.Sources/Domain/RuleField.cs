namespace InkFlow.Modules.Sources.Domain;

/// <summary>
/// 单个输出字段的抽取定义。抽取来源必须是选择器或正则二者之一，
/// 抽取结果再依次经过 <see cref="Transforms"/> 变换。
/// </summary>
public sealed record RuleField(
    string Name,
    RuleSelector? Selector,
    RuleRegex? Regex,
    IReadOnlyList<RuleTransform> Transforms);
