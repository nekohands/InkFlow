using InkFlow.Modules.Library.Application;
using InkFlow.Modules.Crawling.Domain;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Crawling.Application;

/// <summary>
/// 直接地址采集的首个阶段：导入 BookInfo，按既有 v1 匹配策略得到正典书，
/// 再创建带同一 RunId 的 Toc 子任务。
/// </summary>
public sealed class BookInfoSyncTaskHandler(
    SourceCatalogService catalog,
    CanonicalBookMatchingService matching,
    ICrawlerTaskRepository tasks,
    TimeProvider clock,
    CollectionRunService? collectionRuns = null) : ICrawlerTaskExecutor
{
    public async Task<CrawlOutcome> ExecuteAsync(
        CrawlerTask task,
        CancellationToken cancellationToken = default)
    {
        if (!task.Payload.Variables.TryGetValue("bookId", out var externalBookId))
        {
            return CrawlOutcome.Fail($"book info task {task.Id} is missing the required 'bookId' variable.");
        }

        var import = await catalog
            .ImportBookInfoAsync(
                task.Payload.SourceId,
                externalBookId,
                cancellationToken,
                new SourceExecutionContext(
                    task.Payload.SourceId,
                    task.Payload.CredentialReferenceId,
                    SourceCredentialOwnerScope.Platform))
            .ConfigureAwait(false);
        if (!import.IsSuccess)
        {
            return CrawlOutcome.Fail(string.Join("; ", import.Errors));
        }

        var match = await matching
            .CreateOrMatchAsync(task.Payload.SourceId, externalBookId, cancellationToken)
            .ConfigureAwait(false);
        if (!match.IsSuccess || match.Book is null)
        {
            return CrawlOutcome.Fail(string.Join("; ", match.Errors));
        }

        if (task.Payload.RunId is not { } runId || collectionRuns is null)
        {
            return CrawlOutcome.Ok();
        }

        // Stopping/cancelled runs keep the successful source import and match,
        // but must not create future child work.
        if (!await collectionRuns
                .CanScheduleFollowUpAsync(runId, cancellationToken)
                .ConfigureAwait(false))
        {
            return CrawlOutcome.Ok();
        }

        await collectionRuns
            .SetCanonicalBookAsync(runId, match.Book.Id, cancellationToken)
            .ConfigureAwait(false);
        await collectionRuns
            .AdvanceStageAsync(runId, CollectionRunStage.Toc, cancellationToken)
            .ConfigureAwait(false);

        await tasks
            .TryAddIfNoConflictingTaskForCollectionRunAsync(
                CrawlerTask.Create(
                    new CrawlPayload(
                        task.Payload.SourceId,
                        SourceCapability.Toc,
                        new Dictionary<string, string>
                        {
                            ["bookId"] = externalBookId,
                            ["reason"] = "collection-run",
                        },
                        task.Payload.CredentialReferenceId,
                        runId),
                    createdAt: clock.GetUtcNow()),
                runId,
                "bookId",
                externalBookId,
                cancellationToken,
                ignoreDeadLettered: true)
            .ConfigureAwait(false);

        return CrawlOutcome.Ok();
    }
}

/// <summary>
/// Toc 同步处理器:执行目录规则 → 来源章节落库 → (若书目已确认匹配)映射为正典章节
/// → 为从未抓取过正文的新章节联动入队 Content 抓取任务(追更闭环)。
/// </summary>
public sealed class TocSyncTaskHandler(
    SourceCatalogService catalog,
    CanonicalChapterMappingService mappingService,
    ContentFetchChainService contentChain,
    CollectionRunService? collectionRuns = null) : ICrawlerTaskExecutor
{
    public async Task<CrawlOutcome> ExecuteAsync(CrawlerTask task, CancellationToken cancellationToken = default)
    {
        if (!task.Payload.Variables.TryGetValue("bookId", out var externalBookId))
        {
            return CrawlOutcome.Fail($"toc task {task.Id} is missing the required 'bookId' variable.");
        }

        var sync = await catalog
            .SyncChaptersAsync(
                task.Payload.SourceId,
                externalBookId,
                cancellationToken,
                new SourceExecutionContext(
                    task.Payload.SourceId,
                    task.Payload.CredentialReferenceId,
                    SourceCredentialOwnerScope.Platform))
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

        if (task.Payload.RunId is { } runId && collectionRuns is not null)
        {
            // 暂停允许当前 Toc 原子项完成并留下 Pending Content 子任务，
            // 停止/取消则不再安排后续工作。
            if (!await collectionRuns
                    .CanScheduleFollowUpAsync(runId, cancellationToken)
                    .ConfigureAwait(false))
            {
                return CrawlOutcome.Ok();
            }

            await collectionRuns
                .AdvanceStageAsync(runId, CollectionRunStage.Content, cancellationToken)
                .ConfigureAwait(false);
        }

        // 目录与映射落库后立即联动正文抓取:只补"该来源尚未抓取过"的章节,
        // 已有产物/在途任务/死信的章节由链式服务按不变量跳过。
        await contentChain
            .EnqueuePendingContentFetchesAsync(
                task.Payload.SourceId,
                externalBookId,
                cancellationToken,
                task.Payload.CredentialReferenceId,
                task.Payload.RunId)
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
                cancellationToken,
                new SourceExecutionContext(
                    task.Payload.SourceId,
                    task.Payload.CredentialReferenceId,
                    SourceCredentialOwnerScope.Platform))
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
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // 发布基础设施故障 = 任务失败,交由既有重试退避链处理。
                return CrawlOutcome.Fail("content publish failed.");
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
