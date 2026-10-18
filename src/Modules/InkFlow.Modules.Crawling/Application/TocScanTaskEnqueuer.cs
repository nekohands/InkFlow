using InkFlow.Modules.Crawling.Domain;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Crawling.Application;

/// <summary>Owns TOC task construction and the repository's atomic dedupe gate.</summary>
internal sealed class TocScanTaskEnqueuer(ICrawlerTaskRepository taskRepository)
{
    public async Task<bool> TryEnqueueAsync(
        string sourceId,
        string externalBookId,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        var task = CrawlerTask.Create(
            new CrawlPayload(
                sourceId,
                SourceCapability.Toc,
                new Dictionary<string, string> { ["bookId"] = externalBookId }),
            createdAt: createdAt);

        return await taskRepository
            .TryAddIfNoConflictingTaskAsync(
                task,
                "bookId",
                externalBookId,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
