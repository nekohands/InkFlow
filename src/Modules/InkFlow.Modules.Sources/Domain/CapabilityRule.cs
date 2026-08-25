namespace InkFlow.Modules.Sources.Domain;

/// <summary>
/// 一个能力的完整规则：如何发请求、以及如何把响应映射为结构化字段。
/// </summary>
public sealed record CapabilityRule(
    SourceCapability Capability,
    RuleRequest Request,
    IReadOnlyList<RuleField> Fields);
