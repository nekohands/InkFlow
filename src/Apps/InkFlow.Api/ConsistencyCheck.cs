using InkFlow.Modules.Content.Infrastructure.Persistence;
using InkFlow.Modules.Crawling.Domain;
using InkFlow.Modules.Crawling.Infrastructure.Persistence;
using InkFlow.Modules.Library.Domain;
using InkFlow.Modules.Library.Infrastructure.Persistence;
using InkFlow.Modules.Sources.Domain;
using InkFlow.Modules.Sources.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Api;

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

public sealed class ConsistencyCheckService(
    IConsistencySnapshotReader snapshotReader,
    TimeProvider clock) : IConsistencyCheckService
{
    public const int MaxReturnedIssues = 1_000;

    public async Task<ConsistencyCheckReport> CheckAsync(
        CancellationToken cancellationToken = default)
    {
        var snapshot = await snapshotReader
            .ReadAsync(cancellationToken)
            .ConfigureAwait(false);
        var allIssues = ConsistencyCheckValidator.Validate(snapshot);
        var returnedIssues = allIssues.Take(MaxReturnedIssues).ToList();

        return new ConsistencyCheckReport(
            clock.GetUtcNow(),
            allIssues.Count == 0 ? "healthy" : "issues_found",
            allIssues.Count,
            returnedIssues.Count,
            returnedIssues.Count < allIssues.Count,
            returnedIssues);
    }
}

/// <summary>
/// 纯函数一致性规则。所有规则只读、可重复执行；修复动作必须另走受控 Repair seam。
/// </summary>
public static class ConsistencyCheckValidator
{
    public static IReadOnlyList<ConsistencyIssue> Validate(ConsistencySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var issues = new List<ConsistencyIssue>();
        var canonicalBooks = FirstById(snapshot.CanonicalBooks, book => book.Id);
        var canonicalChapters = FirstById(snapshot.CanonicalChapters, chapter => chapter.Id);
        var sources = snapshot.Sources
            .GroupBy(source => source.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var sourceBooks = FirstById(snapshot.SourceBooks, book => book.Id);
        var sourceBooksByIdentity = snapshot.SourceBooks
            .GroupBy(book => new SourceBookIdentity(book.SourceId, book.ExternalBookId))
            .ToDictionary(group => group.Key, group => group.First());
        var sourceChapters = FirstById(snapshot.SourceChapters, chapter => chapter.Id);
        var sourceChaptersByIdentity = snapshot.SourceChapters
            .GroupBy(chapter => new SourceChapterIdentity(chapter.SourceBookId, chapter.ExternalChapterId))
            .ToDictionary(group => group.Key, group => group.First());
        var contentVersions = FirstById(snapshot.ContentVersions, version => version.Id);
        var crawlerTasks = FirstById(snapshot.CrawlerTasks, task => task.Id);

        foreach (var group in snapshot.SourceBooks.GroupBy(
                     book => new SourceBookIdentity(book.SourceId, book.ExternalBookId)))
        {
            if (group.Skip(1).Any())
            {
                Add(
                    "source_book_duplicate_identity",
                    "source_book",
                    group.Key.ToString(),
                    "multiple SourceBook rows share the same (SourceId, ExternalBookId) identity.");
            }
        }

        foreach (var book in snapshot.SourceBooks)
        {
            if (!sources.ContainsKey(book.SourceId))
            {
                Add(
                    "source_book_source_missing",
                    "source_book",
                    SourceBookId(book.SourceId, book.ExternalBookId),
                    $"source book refers to missing source '{book.SourceId}'.");
            }
        }

        foreach (var group in snapshot.SourceChapters.GroupBy(
                     chapter => new SourceChapterIdentity(chapter.SourceBookId, chapter.ExternalChapterId)))
        {
            if (group.Skip(1).Any())
            {
                Add(
                    "source_chapter_duplicate_external_id",
                    "source_chapter",
                    group.Key.ToString(),
                    "multiple SourceChapter rows share the same external chapter identity within a source book.");
            }
        }

        foreach (var group in snapshot.SourceChapters.GroupBy(
                     chapter => new SourceChapterIndexIdentity(chapter.SourceBookId, chapter.Index)))
        {
            if (group.Skip(1).Any())
            {
                Add(
                    "source_chapter_duplicate_index",
                    "source_chapter",
                    group.Key.ToString(),
                    "multiple SourceChapter rows share the same chapter index within a source book.");
            }
        }

        foreach (var chapter in snapshot.SourceChapters)
        {
            if (!sourceBooks.ContainsKey(chapter.SourceBookId))
            {
                Add(
                    "source_chapter_book_missing",
                    "source_chapter",
                    chapter.Id.ToString("D"),
                    $"source chapter refers to missing SourceBook '{chapter.SourceBookId:D}'.");
            }
        }

        foreach (var group in snapshot.CanonicalChapters.GroupBy(
                     chapter => new CanonicalChapterIndexIdentity(chapter.BookId, chapter.Index)))
        {
            if (group.Skip(1).Any())
            {
                Add(
                    "canonical_chapter_duplicate_index",
                    "canonical_chapter",
                    group.Key.ToString(),
                    "multiple CanonicalChapter rows share the same index within a canonical book.");
            }
        }

        foreach (var chapter in snapshot.CanonicalChapters)
        {
            if (!canonicalBooks.ContainsKey(chapter.BookId))
            {
                Add(
                    "canonical_chapter_book_missing",
                    "canonical_chapter",
                    chapter.Id.ToString("D"),
                    $"canonical chapter refers to missing CanonicalBook '{chapter.BookId:D}'.");
            }
        }

        foreach (var artifact in snapshot.FetchArtifacts)
        {
            if (!sources.ContainsKey(artifact.SourceId))
            {
                Add(
                    "fetch_artifact_source_missing",
                    "fetch_artifact",
                    artifact.Id.ToString("D"),
                    $"fetch artifact refers to missing source '{artifact.SourceId}'.");
            }

            var sourceBookIdentity = new SourceBookIdentity(artifact.SourceId, artifact.ExternalBookId);
            if (!sourceBooksByIdentity.TryGetValue(sourceBookIdentity, out var sourceBook))
            {
                Add(
                    "fetch_artifact_book_missing",
                    "fetch_artifact",
                    artifact.Id.ToString("D"),
                    $"fetch artifact refers to missing source book '{SourceBookId(artifact.SourceId, artifact.ExternalBookId)}'.");
                continue;
            }

            if (!sourceChaptersByIdentity.ContainsKey(
                    new SourceChapterIdentity(sourceBook.Id, artifact.ExternalChapterId)))
            {
                Add(
                    "fetch_artifact_chapter_missing",
                    "fetch_artifact",
                    artifact.Id.ToString("D"),
                    $"fetch artifact refers to missing source chapter '{artifact.ExternalChapterId}' in source book '{sourceBook.Id:D}'.");
            }
        }

        foreach (var health in snapshot.SourceCapabilityHealth)
        {
            if (!sources.ContainsKey(health.SourceId))
            {
                Add(
                    "source_health_source_missing",
                    "source_capability_health",
                    HealthId(health),
                    $"capability health refers to missing source '{health.SourceId}'.");
            }

            if (!Enum.IsDefined(typeof(SourceCapability), health.Capability))
            {
                Add(
                    "source_health_capability_invalid",
                    "source_capability_health",
                    HealthId(health),
                    $"capability value '{health.Capability}' is not defined by SourceCapability.");
            }

            if (!Enum.IsDefined(typeof(SourceHealthStatus), health.Status))
            {
                Add(
                    "source_health_status_invalid",
                    "source_capability_health",
                    HealthId(health),
                    $"status value '{health.Status}' is not defined by SourceHealthStatus.");
            }
        }

        foreach (var group in snapshot.MatchCandidates.GroupBy(
                     candidate => new SourceBookIdentity(candidate.SourceId, candidate.ExternalBookId)))
        {
            if (group.Skip(1).Any())
            {
                Add(
                    "match_candidate_duplicate_identity",
                    "match_candidate",
                    group.Key.ToString(),
                    "multiple MatchCandidate rows share the same source book identity.");
            }
        }

        foreach (var candidate in snapshot.MatchCandidates)
        {
            if (!canonicalBooks.ContainsKey(candidate.CanonicalBookId))
            {
                Add(
                    "match_candidate_canonical_book_missing",
                    "match_candidate",
                    candidate.Id.ToString("D"),
                    $"match candidate refers to missing canonical book '{candidate.CanonicalBookId:D}'.");
            }

            if (!sourceBooksByIdentity.ContainsKey(
                    new SourceBookIdentity(candidate.SourceId, candidate.ExternalBookId)))
            {
                Add(
                    "match_candidate_source_book_missing",
                    "match_candidate",
                    candidate.Id.ToString("D"),
                    $"match candidate refers to missing source book '{SourceBookId(candidate.SourceId, candidate.ExternalBookId)}'.");
            }

            if (!Enum.IsDefined(typeof(MatchCandidateStatus), candidate.Status))
            {
                Add(
                    "match_candidate_status_invalid",
                    "match_candidate",
                    candidate.Id.ToString("D"),
                    $"status value '{candidate.Status}' is not defined by MatchCandidateStatus.");
            }
        }

        foreach (var group in snapshot.ChapterMappings.GroupBy(
                     mapping => new MappingIdentity(mapping.SourceId, mapping.ExternalChapterId)))
        {
            if (group.Skip(1).Any())
            {
                Add(
                    "chapter_mapping_duplicate_identity",
                    "chapter_mapping",
                    group.Key.ToString(),
                    "multiple ChapterMapping rows share the same (SourceId, ExternalChapterId) identity.");
            }
        }

        foreach (var mapping in snapshot.ChapterMappings)
        {
            if (!sourceChapters.TryGetValue(mapping.SourceChapterId, out var sourceChapter))
            {
                Add(
                    "chapter_mapping_source_chapter_missing",
                    "chapter_mapping",
                    mapping.Id.ToString("D"),
                    $"chapter mapping refers to missing source chapter '{mapping.SourceChapterId:D}'.");
            }
            else if (sourceBooks.TryGetValue(sourceChapter.SourceBookId, out var sourceBook))
            {
                if (!string.Equals(sourceBook.SourceId, mapping.SourceId, StringComparison.Ordinal))
                {
                    Add(
                        "chapter_mapping_source_id_mismatch",
                        "chapter_mapping",
                        mapping.Id.ToString("D"),
                        $"mapping source '{mapping.SourceId}' does not match source chapter's source '{sourceBook.SourceId}'.");
                }

                if (!string.Equals(sourceChapter.ExternalChapterId, mapping.ExternalChapterId, StringComparison.Ordinal))
                {
                    Add(
                        "chapter_mapping_external_id_mismatch",
                        "chapter_mapping",
                        mapping.Id.ToString("D"),
                        "mapping external chapter ID does not match the referenced SourceChapter.");
                }
            }

            if (!canonicalBooks.ContainsKey(mapping.CanonicalBookId))
            {
                Add(
                    "chapter_mapping_canonical_book_missing",
                    "chapter_mapping",
                    mapping.Id.ToString("D"),
                    $"chapter mapping refers to missing canonical book '{mapping.CanonicalBookId:D}'.");
            }

            if (!canonicalChapters.TryGetValue(mapping.CanonicalChapterId, out var canonicalChapter))
            {
                Add(
                    "chapter_mapping_canonical_chapter_missing",
                    "chapter_mapping",
                    mapping.Id.ToString("D"),
                    $"chapter mapping refers to missing canonical chapter '{mapping.CanonicalChapterId:D}'.");
            }
            else if (canonicalChapter.BookId != mapping.CanonicalBookId)
            {
                Add(
                    "chapter_mapping_canonical_book_mismatch",
                    "chapter_mapping",
                    mapping.Id.ToString("D"),
                    "mapping canonical book does not match the referenced CanonicalChapter parent.");
            }

            if (string.IsNullOrWhiteSpace(mapping.AlignmentAlgorithmVersion) ||
                string.IsNullOrWhiteSpace(mapping.AlignmentEvidence))
            {
                Add(
                    "chapter_mapping_explainability_missing",
                    "chapter_mapping",
                    mapping.Id.ToString("D"),
                    "chapter mapping must retain both alignment algorithm version and evidence.");
            }
        }

        foreach (var group in snapshot.ContentVersions.GroupBy(
                     version => new ContentIdentity(version.CanonicalChapterId, version.CanonicalHash)))
        {
            if (group.Skip(1).Any())
            {
                Add(
                    "content_version_duplicate_hash",
                    "content_version",
                    group.Key.ToString(),
                    "multiple ContentVersion rows share the same canonical hash within a chapter.");
            }
        }

        foreach (var group in snapshot.ContentVersions.Where(version => version.IsCurrent)
                     .GroupBy(version => version.CanonicalChapterId))
        {
            if (group.Skip(1).Any())
            {
                Add(
                    "content_version_multiple_current",
                    "canonical_chapter",
                    group.Key.ToString("D"),
                    "more than one ContentVersion is marked current for the same canonical chapter.");
            }
        }

        foreach (var version in snapshot.ContentVersions)
        {
            if (!sources.ContainsKey(version.SourceId))
            {
                Add(
                    "content_version_source_missing",
                    "content_version",
                    version.Id.ToString("D"),
                    $"content version refers to missing source '{version.SourceId}'.");
            }

            if (!canonicalBooks.ContainsKey(version.CanonicalBookId))
            {
                Add(
                    "content_version_canonical_book_missing",
                    "content_version",
                    version.Id.ToString("D"),
                    $"content version refers to missing canonical book '{version.CanonicalBookId:D}'.");
            }

            if (!canonicalChapters.TryGetValue(version.CanonicalChapterId, out var canonicalChapter))
            {
                Add(
                    "content_version_canonical_chapter_missing",
                    "content_version",
                    version.Id.ToString("D"),
                    $"content version refers to missing canonical chapter '{version.CanonicalChapterId:D}'.");
            }
            else if (canonicalChapter.BookId != version.CanonicalBookId)
            {
                Add(
                    "content_version_canonical_book_mismatch",
                    "content_version",
                    version.Id.ToString("D"),
                    "content version canonical book does not match the referenced CanonicalChapter parent.");
            }

            if (string.IsNullOrWhiteSpace(version.CanonicalHash))
            {
                Add(
                    "content_version_hash_missing",
                    "content_version",
                    version.Id.ToString("D"),
                    "content version must retain a canonical hash.");
            }

            if (version.CanonicalTextLength < 1 || version.ParagraphCount < 1)
            {
                Add(
                    "content_version_payload_invalid",
                    "content_version",
                    version.Id.ToString("D"),
                    "content version must retain non-empty canonical text and a positive paragraph count.");
            }
        }

        foreach (var decision in snapshot.ContentSelectionDecisions)
        {
            if (!canonicalChapters.ContainsKey(decision.CanonicalChapterId))
            {
                Add(
                    "content_selection_chapter_missing",
                    "content_selection_decision",
                    decision.Id.ToString("D"),
                    $"selection decision refers to missing canonical chapter '{decision.CanonicalChapterId:D}'.");
            }

            if (!contentVersions.TryGetValue(decision.SelectedVersionId, out var selectedVersion))
            {
                Add(
                    "content_selection_version_missing",
                    "content_selection_decision",
                    decision.Id.ToString("D"),
                    $"selection decision refers to missing content version '{decision.SelectedVersionId:D}'.");
            }
            else
            {
                if (selectedVersion.CanonicalChapterId != decision.CanonicalChapterId)
                {
                    Add(
                        "content_selection_chapter_mismatch",
                        "content_selection_decision",
                        decision.Id.ToString("D"),
                        "selection decision chapter does not match the selected content version chapter.");
                }

                if (!selectedVersion.IsCurrent)
                {
                    Add(
                        "content_selection_version_not_current",
                        "content_selection_decision",
                        decision.Id.ToString("D"),
                        "the selected version recorded by the decision is not marked current.");
                }
            }

            if (string.IsNullOrWhiteSpace(decision.AlgorithmVersion) ||
                string.IsNullOrWhiteSpace(decision.Evidence))
            {
                Add(
                    "content_selection_explainability_missing",
                    "content_selection_decision",
                    decision.Id.ToString("D"),
                    "selection decision must retain both algorithm version and evidence.");
            }
        }

        foreach (var group in snapshot.ContentSelectionDecisions.GroupBy(
                     decision => decision.CanonicalChapterId))
        {
            var latest = group
                .OrderByDescending(decision => decision.CreatedAt)
                .ThenByDescending(decision => decision.Id)
                .First();

            if (contentVersions.TryGetValue(latest.SelectedVersionId, out var selectedVersion) &&
                !selectedVersion.IsCurrent)
            {
                Add(
                    "content_selection_latest_not_current",
                    "canonical_chapter",
                    latest.CanonicalChapterId.ToString("D"),
                    $"latest selection decision '{latest.Id:D}' does not point to a current content version.");
            }
        }

        foreach (var task in snapshot.CrawlerTasks)
        {
            if (!sources.ContainsKey(task.SourceId))
            {
                Add(
                    "crawler_task_source_missing",
                    "crawler_task",
                    task.Id.ToString("D"),
                    $"crawler task refers to missing source '{task.SourceId}'.");
            }

            if (!Enum.IsDefined(typeof(CrawlerTaskStatus), task.Status))
            {
                Add(
                    "crawler_task_status_invalid",
                    "crawler_task",
                    task.Id.ToString("D"),
                    $"status value '{task.Status}' is not defined by CrawlerTaskStatus.");
            }
        }

        foreach (var deadLetter in snapshot.DeadLetters)
        {
            if (!sources.ContainsKey(deadLetter.SourceId))
            {
                Add(
                    "dead_letter_source_missing",
                    "dead_letter",
                    deadLetter.Id.ToString("D"),
                    $"dead letter refers to missing source '{deadLetter.SourceId}'.");
            }

            if (!crawlerTasks.TryGetValue(deadLetter.TaskId, out var task))
            {
                Add(
                    "dead_letter_task_missing",
                    "dead_letter",
                    deadLetter.Id.ToString("D"),
                    $"dead letter refers to missing crawler task '{deadLetter.TaskId:D}'.");
            }
            else
            {
                if (!string.Equals(task.SourceId, deadLetter.SourceId, StringComparison.Ordinal))
                {
                    Add(
                        "dead_letter_source_mismatch",
                        "dead_letter",
                        deadLetter.Id.ToString("D"),
                        "dead letter source does not match the original crawler task source.");
                }

                if (task.Status != (int)CrawlerTaskStatus.DeadLettered)
                {
                    Add(
                        "dead_letter_task_state_mismatch",
                        "dead_letter",
                        deadLetter.Id.ToString("D"),
                        "a dead letter must reference a crawler task still in DeadLettered state.");
                }
            }

            if (deadLetter.ReplayTaskId is { } replayTaskId && !crawlerTasks.ContainsKey(replayTaskId))
            {
                Add(
                    "dead_letter_replay_task_missing",
                    "dead_letter",
                    deadLetter.Id.ToString("D"),
                    $"dead letter replay history refers to missing crawler task '{replayTaskId:D}'.");
            }
        }

        return issues
            .OrderBy(issue => issue.Code, StringComparer.Ordinal)
            .ThenBy(issue => issue.ResourceType, StringComparer.Ordinal)
            .ThenBy(issue => issue.ResourceId, StringComparer.Ordinal)
            .ThenBy(issue => issue.Message, StringComparer.Ordinal)
            .ToList();

        void Add(string code, string resourceType, string resourceId, string message) =>
            issues.Add(ConsistencyIssue.Error(code, resourceType, resourceId, message));
    }

    private static Dictionary<Guid, T> FirstById<T>(
        IEnumerable<T> values,
        Func<T, Guid> idSelector) =>
        values
            .GroupBy(idSelector)
            .ToDictionary(group => group.Key, group => group.First());

    private static string SourceBookId(string sourceId, string externalBookId) =>
        $"{sourceId}/{externalBookId}";

    private static string HealthId(SourceCapabilityHealthSnapshot health) =>
        $"{health.SourceId}/{health.Capability}";

    private readonly record struct SourceBookIdentity(string SourceId, string ExternalBookId)
    {
        public override string ToString() => $"{SourceId}/{ExternalBookId}";
    }

    private readonly record struct SourceChapterIdentity(Guid SourceBookId, string ExternalChapterId)
    {
        public override string ToString() => $"{SourceBookId:D}/{ExternalChapterId}";
    }

    private readonly record struct SourceChapterIndexIdentity(Guid SourceBookId, int Index)
    {
        public override string ToString() => $"{SourceBookId:D}/{Index}";
    }

    private readonly record struct CanonicalChapterIndexIdentity(Guid BookId, int Index)
    {
        public override string ToString() => $"{BookId:D}/{Index}";
    }

    private readonly record struct MappingIdentity(string SourceId, string ExternalChapterId)
    {
        public override string ToString() => $"{SourceId}/{ExternalChapterId}";
    }

    private readonly record struct ContentIdentity(Guid CanonicalChapterId, string CanonicalHash)
    {
        public override string ToString() => $"{CanonicalChapterId:D}/{CanonicalHash}";
    }
}

/// <summary>
/// PostgreSQL 只读 Adapter：每个模块仍只暴露自己的实体，跨模块比较集中在上层检查器完成。
/// 查询只投影 ID/关系/质量元数据与正文长度，不读取正文内容字段。
/// </summary>
public sealed class EfConsistencySnapshotReader(
    LibraryDbContext library,
    SourcesDbContext sources,
    ContentDbContext content,
    CrawlingDbContext crawling) : IConsistencySnapshotReader
{
    public async Task<ConsistencySnapshot> ReadAsync(
        CancellationToken cancellationToken = default)
    {
        var canonicalBooks = await library.Books
            .AsNoTracking()
            .Select(book => new CanonicalBookSnapshot(book.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var canonicalChapters = await library.Chapters
            .AsNoTracking()
            .Select(chapter => new CanonicalChapterSnapshot(chapter.Id, chapter.BookId, chapter.ChapterIndex))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var matchCandidates = await library.MatchCandidates
            .AsNoTracking()
            .Select(candidate => new MatchCandidateSnapshot(
                candidate.Id,
                candidate.CanonicalBookId,
                candidate.SourceId,
                candidate.ExternalBookId,
                candidate.Status))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var chapterMappings = await library.ChapterMappings
            .AsNoTracking()
            .Select(mapping => new ChapterMappingSnapshot(
                mapping.Id,
                mapping.SourceId,
                mapping.ExternalChapterId,
                mapping.SourceChapterId,
                mapping.CanonicalBookId,
                mapping.CanonicalChapterId,
                mapping.AlignmentAlgorithmVersion,
                mapping.AlignmentEvidence))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var sourceDefinitions = await sources.Sources
            .AsNoTracking()
            .Select(source => new SourceSnapshot(source.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var sourceBooks = await sources.SourceBooks
            .AsNoTracking()
            .Select(book => new SourceBookSnapshot(book.Id, book.SourceId, book.ExternalBookId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var sourceChapters = await sources.SourceChapters
            .AsNoTracking()
            .Select(chapter => new SourceChapterSnapshot(
                chapter.Id,
                chapter.SourceBookId,
                chapter.ExternalChapterId,
                chapter.ChapterIndex))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var fetchArtifacts = await sources.FetchArtifacts
            .AsNoTracking()
            .Select(artifact => new FetchArtifactSnapshot(
                artifact.Id,
                artifact.SourceId,
                artifact.ExternalBookId,
                artifact.ExternalChapterId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var sourceCapabilityHealth = await sources.CapabilityHealth
            .AsNoTracking()
            .Select(health => new SourceCapabilityHealthSnapshot(
                health.SourceId,
                (int)health.Capability,
                (int)health.Status))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var contentVersions = await content.Versions
            .AsNoTracking()
            .Select(version => new ContentVersionSnapshot(
                version.Id,
                version.CanonicalBookId,
                version.CanonicalChapterId,
                version.SourceId,
                version.CanonicalHash,
                version.CanonicalText.Length,
                version.ParagraphCount,
                version.IsCurrent))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var contentSelectionDecisions = await content.SelectionDecisions
            .AsNoTracking()
            .Select(decision => new ContentSelectionDecisionSnapshot(
                decision.Id,
                decision.CanonicalChapterId,
                decision.SelectedVersionId,
                decision.AlgorithmVersion,
                decision.Evidence,
                decision.CreatedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var crawlerTasks = await crawling.Tasks
            .AsNoTracking()
            .Select(task => new CrawlerTaskSnapshot(task.Id, task.SourceId, task.Status))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var deadLetters = await crawling.DeadLetters
            .AsNoTracking()
            .Select(deadLetter => new DeadLetterSnapshot(
                deadLetter.Id,
                deadLetter.TaskId,
                deadLetter.SourceId,
                deadLetter.ReplayTaskId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new ConsistencySnapshot(
            canonicalBooks,
            canonicalChapters,
            sourceDefinitions,
            sourceBooks,
            sourceChapters,
            fetchArtifacts,
            sourceCapabilityHealth,
            matchCandidates,
            chapterMappings,
            contentVersions,
            contentSelectionDecisions,
            crawlerTasks,
            deadLetters);
    }
}
