namespace InkFlow.Modules.Sources.Domain;

/// <summary>
/// 一个能力的完整规则：如何发请求、以及如何把响应映射为结构化输出。
/// 单结果能力(BookInfo/Content)使用 <see cref="Fields"/>；
/// 多结果能力(Toc/Search)通过 <see cref="List"/> 声明条目集抽取；
/// 可选的 <see cref="Pagination"/> 只允许声明受控的同源 next-link、页码或游标链路；
/// 可选的 <see cref="Session"/> 只在本次执行内接收并转发受限响应 Cookie；
/// 可选的 <see cref="ResponseVariables"/> 只为 page-number/cursor 续页请求提供当前响应派生变量；
/// 可选的 <see cref="PreRequests"/> 按声明顺序执行有限的同源前置请求。
/// </summary>
public sealed record CapabilityRule(
    SourceCapability Capability,
    RuleRequest Request,
    IReadOnlyList<RuleField> Fields,
    RuleListBinding? List = null,
    RulePagination? Pagination = null,
    RuleSession? Session = null,
    IReadOnlyList<RuleResponseVariable>? ResponseVariables = null,
    IReadOnlyList<RuleRequestStep>? PreRequests = null);
