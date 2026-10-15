using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Crawling.Domain;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Worker;

/// <summary>按能力分派的执行器组合根。</summary>
internal sealed class CompositeTaskExecutor(
    BookInfoSyncTaskHandler bookInfoHandler,
    TocSyncTaskHandler tocHandler,
    ContentFetchTaskHandler contentHandler) : ICrawlerTaskExecutor
{
    public Task<CrawlOutcome> ExecuteAsync(CrawlerTask task, CancellationToken cancellationToken = default) =>
        task.Payload.Capability switch
        {
            SourceCapability.BookInfo => bookInfoHandler.ExecuteAsync(task, cancellationToken),
            SourceCapability.Toc => tocHandler.ExecuteAsync(task, cancellationToken),
            SourceCapability.Content => contentHandler.ExecuteAsync(task, cancellationToken),
            _ => Task.FromResult(CrawlOutcome.Fail($"capability {task.Payload.Capability} has no handler.")),
        };
}
