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

/// <summary>content.package_jobs 表实体。</summary>
public sealed class BookPackageJobEntity
{
    public Guid Id { get; set; }
    public Guid CanonicalBookId { get; set; }
    public int Format { get; set; }
    public int Status { get; set; }
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; }
    public DateTimeOffset? ScheduledAt { get; set; }
    public string? LeaseOwner { get; set; }
    public DateTimeOffset? LeaseExpiresAt { get; set; }
    public int TotalChapterCount { get; set; }
    public int CompletedChapterCount { get; set; }
    public string? ArtifactFileName { get; set; }
    public string? ArtifactSha256 { get; set; }
    public long? ArtifactLength { get; set; }
    public string? FailureReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
