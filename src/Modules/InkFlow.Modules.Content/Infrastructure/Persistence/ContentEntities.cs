using InkFlow.Modules.Content.Domain;

namespace InkFlow.Modules.Content.Infrastructure.Persistence;

public static class ContentSchema
{
    public const string Name = "content";
}

/// <summary>content_versions 表实体。</summary>
public sealed class ContentVersionEntity
{
    public Guid Id { get; set; }
    public Guid CanonicalBookId { get; set; }
    public Guid CanonicalChapterId { get; set; }
    public string SourceId { get; set; } = null!;
    public string CanonicalHash { get; set; } = null!;
    public string CanonicalText { get; set; } = null!;
    public int ParagraphCount { get; set; }
    public int QualityScore { get; set; }
    public string QualityAlgorithmVersion { get; set; } = null!;
    public string QualityEvidence { get; set; } = null!;
    public bool IsCurrent { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>内容公开策略决策；业务状态由同一本书的最新记录派生。</summary>
public sealed class ContentPolicyDecisionEntity
{
    public Guid Id { get; set; }
    public Guid CanonicalBookId { get; set; }
    public ContentPolicyAction Action { get; set; }
    public string ActorId { get; set; } = null!;
    public string Reason { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
}
