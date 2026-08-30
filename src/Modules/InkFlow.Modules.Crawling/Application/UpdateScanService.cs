using InkFlow.Modules.Crawling.Domain;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Crawling.Application;

/// <summary>
/// 追更扫描:为每个已导入来源书目创建 Toc 同步任务(按书目带阻止性任务去重)。
/// 由 Scheduler 定时调用;Worker 消费任务完成"检测新章 → 抓取 → 落库 → 映射"。
/// </summary>
public sealed class UpdateScanService(
    ISourceBookRepository sourceBooks,
    ICrawlerTaskRepository taskRepository,
    TimeProvider clock,
    ISourceHealthReader? healthReader = null)
{
    public async Task<int> EnqueueTocScansAsync(CancellationToken cancellationToken = default)
    {
        var allBooks = await sourceBooks.ListAllAsync(cancellationToken).ConfigureAwait(false);
        var now = clock.GetUtcNow();
        var enqueued = 0;

        foreach (var book in allBooks)
        {
            if (healthReader is not null && !await healthReader
                    .IsAvailableAsync(book.SourceId, SourceCapability.Toc, cancellationToken)
                    .ConfigureAwait(false))
            {
                continue;
            }

            var task = CrawlerTask.Create(
                new CrawlPayload(
                    book.SourceId,
                    SourceCapability.Toc,
                    new Dictionary<string, string> { ["bookId"] = book.ExternalBookId }),
                createdAt: now);
            if (await taskRepository
                    .TryAddIfNoConflictingTaskAsync(
                        task,
                        "bookId",
                        book.ExternalBookId,
                        cancellationToken)
                    .ConfigureAwait(false))
            {
                enqueued++;
            }
        }

        return enqueued;
    }
}
