using InkFlow.Modules.Crawling.Domain;
using InkFlow.Modules.Library.Application;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Crawling.Application;

/// <summary>
/// 追更正文联动：目录同步 + 正典映射完成后，为"该来源从未抓取过正文"的章节
/// 入队 Content 抓取任务，使"检测新章 → 抓取 → 发布"闭环无需人工介入。
/// 判定规则（全部满足才入队）：
/// 1. 来源书目存在且有章节；
/// 2. Content 能力健康（健康感知，不可用来源不触发上游请求）；
/// 3. 该章节在该来源下没有任何 FetchArtifact（从未成功抓取过）；
/// 4. 无同 (source, content, chapter) 的阻止性任务——Pending/Leased/Running
///    表示已有同工作在途（重试退避链由既有任务承担），DeadLettered 表示已放弃、
///   只能走人工处理路径，禁止被周期扫描反复复活。
/// </summary>
public sealed class ContentFetchChainService(
    ISourceBookRepository sourceBooks,
    IFetchArtifactRepository fetchArtifacts,
    ICrawlerTaskRepository taskRepository,
    TimeProvider clock,
    ISourceHealthReader? healthReader = null)
{
    public async Task<int> EnqueuePendingContentFetchesAsync(
        string sourceId,
        string externalBookId,
        CancellationToken cancellationToken = default)
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
        var fetched = await fetchArtifacts
            .ListFetchedExternalChapterIdsAsync(sourceId, chapterIds, cancellationToken)
            .ConfigureAwait(false);

        var now = clock.GetUtcNow();
        var enqueued = 0;

        foreach (var chapter in book.Chapters)
        {
            if (fetched.Contains(chapter.ExternalChapterId))
            {
                continue;
            }

            if (await taskRepository
                    .HasConflictingTaskAsync(
                        sourceId, SourceCapability.Content, "chapterId", chapter.ExternalChapterId,
                        cancellationToken)
                    .ConfigureAwait(false))
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
                                ["chapterId"] = chapter.ExternalChapterId,
                            }),
                        createdAt: now),
                    cancellationToken)
                .ConfigureAwait(false);
            enqueued++;
        }

        return enqueued;
    }
}
