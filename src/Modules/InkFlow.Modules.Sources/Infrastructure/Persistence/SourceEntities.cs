namespace InkFlow.Modules.Sources.Infrastructure.Persistence;

public static class SourcesSchema
{
    public const string Name = "sources";
}

/// <summary>sources 表实体：Source 聚合的持久化形态，规则文档存 jsonb。</summary>
public sealed class SourceEntity
{
    public string Id { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string BaseUrl { get; set; } = null!;
    public string? RuleDslJson { get; set; }
    public string? DefaultCredentialReferenceId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
