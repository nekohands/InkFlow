namespace InkFlow.BuildingBlocks.Persistence;

public sealed class SourceRecord
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string Kind { get; set; } = "Official";
    public string Status { get; set; } = "Active";
    public string CapabilitiesJson { get; set; } = "[]";
    public Guid? ActiveRuleVersionId { get; set; }
    public double HealthScore { get; set; } = 100;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class SourceRuleVersionRecord
{
    public Guid Id { get; set; }
    public Guid SourceId { get; set; }
    public int Version { get; set; }
    public int SchemaVersion { get; set; }
    public string Status { get; set; } = "Draft";
    public string RuleJson { get; set; } = "{}";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? PublishedAtUtc { get; set; }
}

public sealed class SourceBookRecord
{
    public Guid Id { get; set; }
    public Guid SourceId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? LatestChapterExternalId { get; set; }
    public DateTimeOffset? LastCheckedAtUtc { get; set; }
    public DateTimeOffset? LastUpdatedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class SourceChapterRecord
{
    public Guid Id { get; set; }
    public Guid SourceBookId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public long Sequence { get; set; }
    public DateTimeOffset? PublishedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class BookRecord
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string NormalizedTitle { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string NormalizedAuthor { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = "Unknown";
    public long Revision { get; set; } = 1;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class ChapterRecord
{
    public Guid Id { get; set; }
    public Guid BookId { get; set; }
    public long Sequence { get; set; }
    public int? DisplayNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string NormalizedTitle { get; set; } = string.Empty;
    public long Revision { get; set; } = 1;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class SourceBookMatchRecord
{
    public Guid Id { get; set; }
    public Guid BookId { get; set; }
    public Guid SourceBookId { get; set; }
    public double Score { get; set; }
    public string EvidenceJson { get; set; } = "[]";
    public string AlgorithmVersion { get; set; } = "book-match-v1";
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class ChapterMappingRecord
{
    public Guid Id { get; set; }
    public Guid ChapterId { get; set; }
    public Guid SourceChapterId { get; set; }
    public double Score { get; set; }
    public string EvidenceJson { get; set; } = "[]";
    public string AlgorithmVersion { get; set; } = "chapter-align-v1";
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class ContentBlobRecord
{
    public Guid Id { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public string StorageKind { get; set; } = "Inline";
    public string? InlineContent { get; set; }
    public string? ObjectKey { get; set; }
    public long SizeBytes { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class ContentVersionRecord
{
    public Guid Id { get; set; }
    public Guid ChapterId { get; set; }
    public Guid SourceChapterId { get; set; }
    public Guid BlobId { get; set; }
    public string RawHash { get; set; } = string.Empty;
    public string CanonicalHash { get; set; } = string.Empty;
    public double QualityScore { get; set; }
    public string EvidenceJson { get; set; } = "[]";
    public string NormalizerVersion { get; set; } = "normalizer-v1";
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class ChapterSelectionRecord
{
    public Guid ChapterId { get; set; }
    public Guid ContentVersionId { get; set; }
    public bool IsLocked { get; set; }
    public string Reason { get; set; } = "quality-engine";
    public DateTimeOffset SelectedAtUtc { get; set; }
}

public sealed class FetchArtifactRecord
{
    public Guid Id { get; set; }
    public Guid? CrawlerTaskId { get; set; }
    public Guid SourceId { get; set; }
    public Guid? SourceChapterId { get; set; }
    public Guid? RuleVersionId { get; set; }
    public string Url { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string HeadersJson { get; set; } = "{}";
    public string RawHash { get; set; } = string.Empty;
    public string? RawBody { get; set; }
    public string ParserVersion { get; set; } = "rule-adapter-v1";
    public DateTimeOffset FetchedAtUtc { get; set; }
}
