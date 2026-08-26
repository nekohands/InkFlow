namespace InkFlow.Modules.Sources.Domain;

/// <summary>
/// 规则型来源（RuleAdapter）的声明式 DSL 文档根。
/// <paramref name="schemaVersion"/> 决定文档结构与语义；已发布的规则版本不可修改，变更创建新版本。
/// </summary>
public sealed record SourceRuleDsl(
    string SchemaVersion,
    string SourceId,
    IReadOnlyList<CapabilityRule> Rules);
