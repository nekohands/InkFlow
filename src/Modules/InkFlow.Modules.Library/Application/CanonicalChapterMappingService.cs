using InkFlow.Modules.Library.Domain;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Library.Application;

public sealed record ChapterMappingOutcome(
    bool IsSuccess,
    CanonicalBook? Book,
    int NewlyMappedCount,
    IReadOnlyList<string> Errors)
{
    public static ChapterMappingOutcome Ok(CanonicalBook book, int newlyMapped) =>
        new(true, book, newlyMapped, []);
}

/// <summary>
/// 章节映射服务：来源目录同步后，为每个未映射的 SourceChapter 复用或创建稳定的
/// CanonicalChapter 并写入映射记录。相同正典书的不同来源优先按章节序号+规范化标题
/// 对齐，避免第二来源为同一逻辑章节创建重复身份。
/// 幂等保证：重复调用不产生新的正典章节或映射。
/// 前置条件：书目级匹配已完成（存在 Confirmed 候选）。
/// </summary>
public sealed class CanonicalChapterMappingService(
    ISourceBookRepository sourceBookRepository,
    IMatchCandidateRepository matchCandidateRepository,
    ICanonicalBookRepository canonicalBookRepository,
    IChapterMappingRepository chapterMappingRepository)
{
    private readonly TimeProvider _clock = TimeProvider.System;

    public async Task<ChapterMappingOutcome> SyncChapterMappingAsync(
        string sourceId, string externalBookId, CancellationToken cancellationToken = default)
    {
        // 1. 书目级匹配必须已完成。
        var candidate = await matchCandidateRepository
            .FindForSourceBookAsync(sourceId, externalBookId, cancellationToken)
            .ConfigureAwait(false);

        if (candidate is not { Status: MatchCandidateStatus.Confirmed })
        {
            return new ChapterMappingOutcome(false, null, 0,
                [$"mapping: book '{sourceId}/{externalBookId}' has no confirmed canonical match yet."]);
        }

        var canonicalBook = await canonicalBookRepository
            .GetAsync(candidate.CanonicalBookId, cancellationToken)
            .ConfigureAwait(false);

        if (canonicalBook is null)
        {
            return new ChapterMappingOutcome(false, null, 0,
                [$"mapping: canonical book {candidate.CanonicalBookId} does not exist."]);
        }

        var sourceBook = await sourceBookRepository
            .GetAsync(sourceId, externalBookId, cancellationToken)
            .ConfigureAwait(false);

        if (sourceBook is null)
        {
            return new ChapterMappingOutcome(false, null, 0,
                [$"mapping: source book '{sourceId}/{externalBookId}' does not exist."]);
        }

        // 2. 逐章节幂等映射。
        var now = _clock.GetUtcNow();
        var newlyMapped = 0;

        foreach (var sourceChapter in sourceBook.Chapters)
        {
            var existing = await chapterMappingRepository
                .FindAsync(sourceId, sourceChapter.ExternalChapterId, cancellationToken)
                .ConfigureAwait(false);

            if (existing is not null)
            {
                continue;
            }

            var alignment = FindAlignedChapter(canonicalBook, sourceChapter);
            var canonicalChapter = alignment.Chapter ?? canonicalBook.AddChapter(
                canonicalBook.Chapters.Count, sourceChapter.Title, now);
            var mapping = new ChapterMapping(
                Guid.NewGuid(),
                sourceId,
                sourceChapter.ExternalChapterId,
                sourceChapter.Id,
                canonicalBook.Id,
                canonicalChapter.Id,
                now,
                ChapterAlignmentAlgorithm.Version,
                alignment.Evidence);

            await canonicalBookRepository.SaveAsync(canonicalBook, cancellationToken).ConfigureAwait(false);
            await chapterMappingRepository.AddAsync(mapping, cancellationToken).ConfigureAwait(false);
            newlyMapped++;
        }

        return ChapterMappingOutcome.Ok(canonicalBook, newlyMapped);
    }

    private static (CanonicalChapter? Chapter, string Evidence) FindAlignedChapter(
        CanonicalBook canonicalBook, SourceChapter sourceChapter)
    {
        var normalizedTitle = NormalizeTitle(sourceChapter.Title);

        // 最强信号：同一目录序号且标题规范化后相同。
        var samePosition = canonicalBook.Chapters.FirstOrDefault(chapter =>
            chapter.Index == sourceChapter.Index &&
            NormalizeTitle(chapter.Title) == normalizedTitle);
        if (samePosition is not null)
        {
            return (
                samePosition,
                $"source-index={sourceChapter.Index};canonical-index={samePosition.Index};normalized-title");
        }

        // 处理某来源插入/缺失少量章节后的序号偏移；标题必须唯一，避免误合并。
        var sameTitle = canonicalBook.Chapters
            .Where(chapter => NormalizeTitle(chapter.Title) == normalizedTitle)
            .ToList();
        if (sameTitle.Count == 1)
        {
            var match = sameTitle[0];
            return (
                match,
                $"source-index={sourceChapter.Index};canonical-index={match.Index};unique-normalized-title");
        }

        return (
            null,
            $"source-index={sourceChapter.Index};new-canonical-chapter");
    }

    private static string NormalizeTitle(string title) =>
        string.Concat(title.Where(c => !char.IsWhiteSpace(c) && !char.IsPunctuation(c)))
            .ToLowerInvariant();
}
