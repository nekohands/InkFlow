using InkFlow.Modules.Crawling.Domain;

namespace InkFlow.Modules.Crawling.Application;

/// <summary>
/// 统一的任务执行 seam。事件驱动领取和周期兜底轮询共享同一失败/死信状态机，
/// 避免两条入口对任务事实产生不同语义。
/// </summary>
public interface ICrawlerTaskProcessor
{
    Task ProcessAsync(CrawlerTask task, CancellationToken cancellationToken = default);
}
