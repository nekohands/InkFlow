namespace InkFlow.Modules.Operations.Application;

public sealed record CanonicalBookSnapshot(Guid Id);

public sealed record CanonicalChapterSnapshot(Guid Id, Guid BookId, int Index);

public sealed record SourceSnapshot(string Id);

public sealed record SourceBookSnapshot(Guid Id, string SourceId, string ExternalBookId);

public sealed record SourceChapterSnapshot(
    Guid Id,
    Guid SourceBookId,
    string ExternalChapterId,
    int Index);

public sealed record FetchArtifactSnapshot(
    Guid Id,
    string SourceId,
    string ExternalBookId,
    string ExternalChapterId);

public sealed record SourceCapabilityHealthSnapshot(string SourceId, int Capability, int Status);

public sealed record MatchCandidateSnapshot(
    Guid Id,
    Guid CanonicalBookId,
    string SourceId,
    string ExternalBookId,
    int Status);

public sealed record ChapterMappingSnapshot(
    Guid Id,
    string SourceId,
    string ExternalChapterId,
    Guid SourceChapterId,
    Guid CanonicalBookId,
    Guid CanonicalChapterId,
    string AlignmentAlgorithmVersion,
    string AlignmentEvidence);

public sealed record ContentVersionSnapshot(
    Guid Id,
    Guid CanonicalBookId,
    Guid CanonicalChapterId,
    string SourceId,
    string CanonicalHash,
    int CanonicalTextLength,
    int ParagraphCount,
    bool IsCurrent);

public sealed record ContentSelectionDecisionSnapshot(
    Guid Id,
    Guid CanonicalChapterId,
    Guid SelectedVersionId,
    string AlgorithmVersion,
    string Evidence,
    DateTimeOffset CreatedAt);

public sealed record CrawlerTaskSnapshot(Guid Id, string SourceId, int Status);

public sealed record DeadLetterSnapshot(
    Guid Id,
    Guid TaskId,
    string SourceId,
    Guid? ReplayTaskId);

/// <summary>
/// 一次一致性扫描所需的最小跨模块只读快照。不携带正文，也不把 EF 实体泄漏给检查器。
/// </summary>
public sealed record ConsistencySnapshot(
    IReadOnlyList<CanonicalBookSnapshot> CanonicalBooks,
    IReadOnlyList<CanonicalChapterSnapshot> CanonicalChapters,
    IReadOnlyList<SourceSnapshot> Sources,
    IReadOnlyList<SourceBookSnapshot> SourceBooks,
    IReadOnlyList<SourceChapterSnapshot> SourceChapters,
    IReadOnlyList<FetchArtifactSnapshot> FetchArtifacts,
    IReadOnlyList<SourceCapabilityHealthSnapshot> SourceCapabilityHealth,
    IReadOnlyList<MatchCandidateSnapshot> MatchCandidates,
    IReadOnlyList<ChapterMappingSnapshot> ChapterMappings,
    IReadOnlyList<ContentVersionSnapshot> ContentVersions,
    IReadOnlyList<ContentSelectionDecisionSnapshot> ContentSelectionDecisions,
    IReadOnlyList<CrawlerTaskSnapshot> CrawlerTasks,
    IReadOnlyList<DeadLetterSnapshot> DeadLetters)
{
    public static ConsistencySnapshot Empty => new(
        [], [], [], [], [], [], [], [], [], [], [], [], []);
}

/// <summary>跨模块一致性扫描的只读快照 Adapter seam。</summary>
public interface IConsistencySnapshotReader
{
    Task<ConsistencySnapshot> ReadAsync(CancellationToken cancellationToken = default);
}

/// <summary>Repair/Operations 查询侧的深接口：调用方只需请求一次可解释报告。</summary>
public interface IConsistencyCheckService
{
    Task<ConsistencyCheckReport> CheckAsync(CancellationToken cancellationToken = default);
}

public sealed record ConsistencyIssue(
    string Code,
    string Severity,
    string ResourceType,
    string ResourceId,
    string Message)
{
    public static ConsistencyIssue Error(
        string code,
        string resourceType,
        string resourceId,
        string message) =>
        new(code, "error", resourceType, resourceId, message);
}

public sealed record ConsistencyCheckReport(
    DateTimeOffset CheckedAt,
    string Status,
    int TotalIssueCount,
    int ReturnedIssueCount,
    bool Truncated,
    IReadOnlyList<ConsistencyIssue> Issues)
{
    public bool IsHealthy => TotalIssueCount == 0;
}

