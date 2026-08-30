using InkFlow.Modules.Crawling.Domain;
using InkFlow.Modules.Library.Application;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Crawling.Application;

/// <summary>
/// 追更正文联动：目录同步 + 正典映射完成后，为"该来源需要抓取正文"的章节
/// 入队 Content 抓取任务，使"检测新章 → 抓取 → 发布"闭环无需人工介入。
/// 需要抓取 = 满足其一：
/// 1. 该章节在该来源下没有任何 FetchArtifact（从未抓取过的新章）；
/// 2. 最新产物已过期（复检时间早于 now - StaleAfter）——修订重扫，上游文本
///    变化会经发布链路产生新的 ContentVersion（版本追加不覆盖），未变化则
///    复检行本身续期保鲜锚点。
/// 且全部满足以下约束：
/// a. 来源书目存在且有章节；
/// b. Content 能力健康（健康感知，不可用来源不触发上游请求）；
/// c. 无同 (source, content, chapter) 的阻止性任务——Pending/Leased/Running
///    表示已有同工作在途（重试退避链由既有任务承担），DeadLettered 表示
///    已放弃、只能走人工处理路径，禁止被周期扫描反复复活；死信章节将在
///    下一个保鲜周期后随 stale 判定自然获得一次重新入队机会（非无限复活）。
/// </summary>
public sealed class ContentFetchChainService(
    ISourceBookRepository sourceBooks,
    IFetchArtifactRepository fetchArtifacts,
    ICrawlerTaskRepository taskRepository,
    TimeProvider clock,
    ISourceHealthReader? healthReader = null,
    TimeSpan? staleAfter = null,
    CollectionRunService? collectionRuns = null)
{
    /// <summary>已抓章节的保鲜期：超过此时长的产物视为过期，允许修订重扫。</summary>
    public static readonly TimeSpan DefaultStaleAfter = TimeSpan.FromDays(7);

    private readonly TimeSpan _staleAfter = staleAfter ?? DefaultStaleAfter;

    public async Task<int> EnqueuePendingContentFetchesAsync(
        string sourceId,
        string externalBookId,
        CancellationToken cancellationToken = default,
        string? credentialReferenceId = null,
        Guid? runId = null)
    {
        var book = await sourceBooks
            .GetAsync(sourceId, externalBookId, cancellationToken)
            .ConfigureAwait(false);

        if (book is null || book.Chapters.Count == 0)
        {
            return 0;
        }

        if (healthReader is not null && !await healthReader
                .IsAvailableAsync(sourceId, SourceCapability.Content, cancellationToken)
                .ConfigureAwait(false))
        {
            return 0;
        }

        var chapterIds = book.Chapters.Select(c => c.ExternalChapterId).ToArray();
        var now = clock.GetUtcNow();

        var fetched = await fetchArtifacts
            .ListFetchedExternalChapterIdsAsync(sourceId, chapterIds, cancellationToken)
            .ConfigureAwait(false);
        var fresh = await fetchArtifacts
            .ListRecentlyFetchedExternalChapterIdsAsync(
                sourceId, chapterIds, since: now - _staleAfter, cancellationToken)
            .ConfigureAwait(false);

        var enqueued = 0;
        var forceRefresh = runId is not null;
        foreach (var chapter in book.Chapters)
        {
            if (runId is { } activeRunId && collectionRuns is not null &&
                !await collectionRuns
                    .CanScheduleFollowUpAsync(activeRunId, cancellationToken)
                    .ConfigureAwait(false))
            {
                break;
            }

            var id = chapter.ExternalChapterId;
            var neverFetched = !fetched.Contains(id);
            var stale = fetched.Contains(id) && !fresh.Contains(id);
            if (!forceRefresh && !neverFetched && !stale)
            {
                continue;
            }

            var hasConflict = runId is null
                ? await taskRepository
                    .HasConflictingTaskAsync(
                        sourceId, SourceCapability.Content, "chapterId", id,
                        cancellationToken)
                    .ConfigureAwait(false)
                : await taskRepository
                    .HasBlockingTaskForCollectionRunAsync(
                        sourceId, SourceCapability.Content, "chapterId", id,
                        cancellationToken,
                        ignoreDeadLettered: true)
                    .ConfigureAwait(false);
            if (hasConflict)
            {
                continue;
            }

            await taskRepository
                .AddAsync(
                    CrawlerTask.Create(
                        new CrawlPayload(
                            sourceId,
                            SourceCapability.Content,
                            new Dictionary<string, string>
                            {
                                ["bookId"] = externalBookId,
                                ["chapterId"] = id,
                                ["reason"] = forceRefresh
                                    ? "collection-run"
                                    : neverFetched ? "new" : "refetch",
                            },
                            credentialReferenceId,
                            runId),
                        createdAt: now),
                    cancellationToken)
                .ConfigureAwait(false);
            enqueued++;
        }

        return enqueued;
    }
}
