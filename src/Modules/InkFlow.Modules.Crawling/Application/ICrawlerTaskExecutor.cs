using InkFlow.Modules.Crawling.Domain;

namespace InkFlow.Modules.Crawling.Application;

/// <summary>单次抓取执行结果。</summary>
public sealed record CrawlOutcome(bool Succeeded, string? FailureReason = null)
{
    public static CrawlOutcome Ok() => new(true);
    public static CrawlOutcome Fail(string reason) => new(false, reason);
}

/// <summary>
/// 任务执行契约：worker 轮询到可执行任务后调用。
/// 实现方负责把 CrawlPayload 翻译成 RuleAdapter/CodeAdapter 调用；
/// 本接口不关心适配器类型（Rule 或 Code 对调度层透明）。
/// </summary>
public interface ICrawlerTaskExecutor
{
    Task<CrawlOutcome> ExecuteAsync(CrawlerTask task, CancellationToken cancellationToken = default);
}
