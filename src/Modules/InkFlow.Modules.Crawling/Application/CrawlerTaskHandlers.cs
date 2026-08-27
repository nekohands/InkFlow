using InkFlow.Modules.Library.Application;
using InkFlow.Modules.Crawling.Domain;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Crawling.Application;

/// <summary>
/// Toc 同步处理器:执行目录规则 → 来源章节落库 → (若书目已确认匹配)映射为正典章节
/// → 为从未抓取过正文的新章节联动入队 Content 抓取任务(追更闭环)。
/// </summary>
public sealed class TocSyncTaskHandler(
    SourceCatalogService catalog,
    CanonicalChapterMappingService mappingService,
    ContentFetchChainService contentChain) : ICrawlerTaskExecutor
{
    public async Task<CrawlOutcome> ExecuteAsync(CrawlerTask task, CancellationToken cancellationToken = default)
    {
        if (!task.Payload.Variables.TryGetValue("bookId", out var externalBookId))
        {
            return CrawlOutcome.Fail($"toc task {task.Id} is missing the required 'bookId' variable.");
        }

        var sync = await catalog
            .SyncChaptersAsync(task.Payload.SourceId, externalBookId, cancellationToken)
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

        // 目录与映射落库后立即联动正文抓取:只补"该来源尚未抓取过"的章节,
        // 已有产物/在途任务/死信的章节由链式服务按不变量跳过。
        await contentChain
            .EnqueuePendingContentFetchesAsync(task.Payload.SourceId, externalBookId, cancellationToken)
            .ConfigureAwait(false);

        return CrawlOutcome.Ok();
    }
}

/// <summary>
/// 正文抓取处理器:执行 Content 规则并按 RawHash 幂等落库;
/// 成功后把原文交给发布桥(若装配),经正典映射产出/更新 IsCurrent 版本。
/// 发布失败视为任务失败走重试退避;不可发布(未映射)静默完成。
/// </summary>
public sealed class ContentFetchTaskHandler(
    SourceContentService contentService,
    IChainedContentPublisher? publisher = null) : ICrawlerTaskExecutor
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
                cancellationToken)
            .ConfigureAwait(false);

        if (!outcome.IsSuccess)
        {
            return CrawlOutcome.Fail(string.Join("; ", outcome.Errors));
        }

        if (publisher is not null && outcome.RawContent is not null)
        {
            bool published;

            try
            {
                published = await publisher.TryPublishAsync(
                        task.Payload.SourceId,
                        externalBookId ?? string.Empty,
                        externalChapterId,
                        outcome.RawContent,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // 发布基础设施故障 = 任务失败,交由既有重试退避链处理。
                return CrawlOutcome.Fail(
                    $"content task {task.Id} publish failed for chapter '{externalChapterId}': {ex.Message}");
            }

            if (!published)
            {
                Console.WriteLine(
                    $"content task {task.Id}: chapter '{externalChapterId}' fetched but not publishable yet (no canonical mapping); skipped publishing.");
            }
        }

        return CrawlOutcome.Ok();
    }
}
