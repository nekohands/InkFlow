using InkFlow.Modules.Content.Infrastructure.Persistence;
using InkFlow.Modules.Crawling.Infrastructure.Persistence;
using InkFlow.Modules.Library.Infrastructure.Persistence;
using InkFlow.Modules.Sources.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

using InkFlow.Modules.Operations.Application;

namespace InkFlow.Api;

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
