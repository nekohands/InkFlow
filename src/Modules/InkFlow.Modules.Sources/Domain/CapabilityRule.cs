namespace InkFlow.Modules.Sources.Domain;

/// <summary>
/// 一个能力的完整规则：如何发请求、以及如何把响应映射为结构化输出。
/// 单结果能力(BookInfo/Content)使用 <see cref="Fields"/>；
/// 多结果能力(Toc/Search)通过 <see cref="List"/> 声明条目集抽取；
/// 可选的 <see cref="Pagination"/> 只允许声明受控的同源 next-link、页码或游标链路。
/// </summary>
public sealed record CapabilityRule(
    SourceCapability Capability,
    RuleRequest Request,
    IReadOnlyList<RuleField> Fields,
    RuleListBinding? List = null,
    RulePagination? Pagination = null);
