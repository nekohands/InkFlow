using InkFlow.Modules.Library.Application;
using InkFlow.Modules.Crawling.Domain;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Crawling.Application;

/// <summary>
/// Toc 同步处理器:执行目录规则 → 来源章节落库 → (若书目已确认匹配)映射为正典章节。
/// </summary>
public sealed class TocSyncTaskHandler(
    SourceCatalogService catalog,
    CanonicalChapterMappingService mappingService,
    TimeProvider clock) : ICrawlerTaskExecutor
{
    public async Task<CrawlOutcome> ExecuteAsync(CrawlerTask task, CancellationToken cancellationToken = default)
    {
        if (!task.Payload.Variables.TryGetValue("bookId", out var externalBookId))
        {
            return CrawlOutcome.Fail($"toc task {task.Id} is missing the required 'bookId' variable.");
        }

        var sync = await catalog
            .SyncChaptersAsync(task.Payload.SourceId, externalBookId, clock.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);

        if (!sync.IsSuccess)
        {
            return CrawlOutcome.Fail(string.Join("; ", sync.Errors));
        }

        var mapping = await mappingService
            .SyncChapterMappingAsync(task.Payload.SourceId, externalBookId, cancellationToken)
            .ConfigureAwait(false);

        if (!mapping.IsSuccess)
        {
            return CrawlOutcome.Fail(string.Join("; ", mapping.Errors));
        }

        return CrawlOutcome.Ok();
    }
}

/// <summary>
/// 正文抓取处理器:执行 Content 规则并按 RawHash 幂等落库。
/// </summary>
public sealed class ContentFetchTaskHandler(
    SourceContentService contentService,
    TimeProvider clock) : ICrawlerTaskExecutor
{
    public async Task<CrawlOutcome> ExecuteAsync(CrawlerTask task, CancellationToken cancellationToken = default)
    {
        if (!task.Payload.Variables.TryGetValue("chapterId", out var externalChapterId))
        {
            return CrawlOutcome.Fail($"content task {task.Id} is missing the required 'chapterId' variable.");
        }

        task.Payload.Variables.TryGetValue("bookId", out var externalBookId);

        var outcome = await contentService
            .FetchChapterContentAsync(
                task.Payload.SourceId, externalBookId ?? string.Empty, externalChapterId,
                clock.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);

        if (!outcome.IsSuccess)
        {
            return CrawlOutcome.Fail(string.Join("; ", outcome.Errors));
        }

        return CrawlOutcome.Ok();
    }
}
