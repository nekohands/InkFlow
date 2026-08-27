using InkFlow.Api;
using InkFlow.Modules.Crawling.Domain;
using InkFlow.Modules.Library.Domain;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class ConsistencyCheckTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 28, 16, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Healthy_snapshot_produces_a_healthy_report()
    {
        var bookId = Guid.NewGuid();
        var chapterId = Guid.NewGuid();
        var sourceBookId = Guid.NewGuid();
        var sourceChapterId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var snapshot = new ConsistencySnapshot(
            [new CanonicalBookSnapshot(bookId)],
            [new CanonicalChapterSnapshot(chapterId, bookId, 0)],
            [new SourceSnapshot("source-a")],
            [new SourceBookSnapshot(sourceBookId, "source-a", "book-a")],
            [new SourceChapterSnapshot(sourceChapterId, sourceBookId, "chapter-a", 0)],
            [],
            [],
            [new MatchCandidateSnapshot(
                Guid.NewGuid(),
                bookId,
                "source-a",
                "book-a",
                (int)MatchCandidateStatus.Confirmed)],
            [new ChapterMappingSnapshot(
                Guid.NewGuid(),
                "source-a",
                "chapter-a",
                sourceChapterId,
                bookId,
                chapterId,
                "chapter-alignment-v1",
                "source-index=0")],
            [new ContentVersionSnapshot(
                versionId,
                bookId,
                chapterId,
                "source-a",
                "hash-a",
                CanonicalTextLength: 12,
                ParagraphCount: 1,
                IsCurrent: true)],
            [new ContentSelectionDecisionSnapshot(
                Guid.NewGuid(),
                chapterId,
                versionId,
                "content-selection-v1",
                "selected=version-a",
                T0)],
            [new CrawlerTaskSnapshot(taskId, "source-a", (int)CrawlerTaskStatus.Pending)],
            []);

        var service = new ConsistencyCheckService(
            new FixedSnapshotReader(snapshot),
            new FixedClock(T0));

        var report = await service.CheckAsync();

        Assert.IsTrue(report.IsHealthy);
        Assert.AreEqual("healthy", report.Status);
        Assert.AreEqual(0, report.TotalIssueCount);
        Assert.AreEqual(0, report.ReturnedIssueCount);
        Assert.IsFalse(report.Truncated);
        Assert.AreEqual(T0, report.CheckedAt);
    }

    [TestMethod]
    public async Task Cross_module_orphans_and_mismatches_are_explained_by_stable_codes()
    {
        var bookId = Guid.NewGuid();
        var chapterId = Guid.NewGuid();
        var sourceBookId = Guid.NewGuid();
        var sourceChapterId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var missingTaskId = Guid.NewGuid();
        var snapshot = new ConsistencySnapshot(
            [new CanonicalBookSnapshot(bookId)],
            [new CanonicalChapterSnapshot(chapterId, bookId, 0)],
            [new SourceSnapshot("source-a")],
            [new SourceBookSnapshot(sourceBookId, "source-a", "book-a")],
            [new SourceChapterSnapshot(sourceChapterId, sourceBookId, "chapter-a", 0)],
            [new FetchArtifactSnapshot(Guid.NewGuid(), "source-a", "book-a", "missing-chapter")],
            [],
            [new MatchCandidateSnapshot(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "source-a",
                "missing-book",
                (int)MatchCandidateStatus.Confirmed)],
            [new ChapterMappingSnapshot(
                Guid.NewGuid(),
                "source-b",
                "wrong-chapter",
                sourceChapterId,
                Guid.NewGuid(),
                chapterId,
                "",
                "")],
            [new ContentVersionSnapshot(
                versionId,
                Guid.NewGuid(),
                chapterId,
                "source-a",
                "hash-a",
                CanonicalTextLength: 12,
                ParagraphCount: 1,
                IsCurrent: false)],
            [new ContentSelectionDecisionSnapshot(
                Guid.NewGuid(),
                chapterId,
                versionId,
                "content-selection-v1",
                "selected=version-a",
                T0)],
            [new CrawlerTaskSnapshot(Guid.NewGuid(), "missing-source", 999)],
            [new DeadLetterSnapshot(Guid.NewGuid(), missingTaskId, "missing-source", Guid.NewGuid())]);

        var report = await new ConsistencyCheckService(
            new FixedSnapshotReader(snapshot),
            new FixedClock(T0)).CheckAsync();
        var codes = report.Issues.Select(issue => issue.Code).ToHashSet(StringComparer.Ordinal);

        Assert.IsFalse(report.IsHealthy);
        CollectionAssert.IsSubsetOf(
            new[]
            {
                "fetch_artifact_chapter_missing",
                "match_candidate_canonical_book_missing",
                "match_candidate_source_book_missing",
                "chapter_mapping_source_id_mismatch",
                "chapter_mapping_external_id_mismatch",
                "chapter_mapping_canonical_book_missing",
                "chapter_mapping_explainability_missing",
                "content_version_canonical_book_mismatch",
                "content_selection_version_not_current",
                "content_selection_latest_not_current",
                "crawler_task_source_missing",
                "crawler_task_status_invalid",
                "dead_letter_task_missing",
                "dead_letter_source_missing",
                "dead_letter_replay_task_missing",
            },
            codes.ToList());
        Assert.AreEqual("issues_found", report.Status);
    }

    [TestMethod]
    public async Task Report_caps_returned_issues_but_preserves_total_count()
    {
        var tasks = Enumerable.Range(0, ConsistencyCheckService.MaxReturnedIssues + 1)
            .Select(_ => new CrawlerTaskSnapshot(Guid.NewGuid(), "missing-source", (int)CrawlerTaskStatus.Pending))
            .ToList();
        var snapshot = ConsistencySnapshot.Empty with { CrawlerTasks = tasks };

        var report = await new ConsistencyCheckService(
            new FixedSnapshotReader(snapshot),
            new FixedClock(T0)).CheckAsync();

        Assert.AreEqual(ConsistencyCheckService.MaxReturnedIssues + 1, report.TotalIssueCount);
        Assert.AreEqual(ConsistencyCheckService.MaxReturnedIssues, report.ReturnedIssueCount);
        Assert.IsTrue(report.Truncated);
        Assert.AreEqual(ConsistencyCheckService.MaxReturnedIssues, report.Issues.Count);
    }

    private sealed class FixedSnapshotReader(ConsistencySnapshot snapshot) : IConsistencySnapshotReader
    {
        public Task<ConsistencySnapshot> ReadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(snapshot);
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
