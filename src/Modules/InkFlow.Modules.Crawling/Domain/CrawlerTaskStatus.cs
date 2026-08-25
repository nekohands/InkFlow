namespace InkFlow.Modules.Crawling.Domain;

public enum CrawlerTaskStatus
{
    /// <summary>待领取。</summary>
    Pending,
    /// <summary>已被某个 worker 通过租约持有，尚未开始执行。</summary>
    Leased,
    /// <summary>正在执行。</summary>
    Running,
    /// <summary>成功完成（终态）。</summary>
    Completed,
    /// <summary>本次尝试失败；若未达重试上限将回到 Pending，否则进入死信。</summary>
    Failed,
    /// <summary>重试耗尽后的终态（终态），进入死信队列等待人工/修复流程。</summary>
    DeadLettered,
}

/// <summary>状态机允许的流转。非法流转一律抛出，保证聚合不变量。</summary>
public static class CrawlerTaskTransitions
{
    private static readonly Dictionary<CrawlerTaskStatus, CrawlerTaskStatus[]> Allowed = new()
    {
        [CrawlerTaskStatus.Pending] = [CrawlerTaskStatus.Leased],
        [CrawlerTaskStatus.Leased] = [CrawlerTaskStatus.Running, CrawlerTaskStatus.Pending],
        [CrawlerTaskStatus.Running] = [CrawlerTaskStatus.Completed, CrawlerTaskStatus.Failed],
        [CrawlerTaskStatus.Failed] = [CrawlerTaskStatus.Pending, CrawlerTaskStatus.DeadLettered],
        [CrawlerTaskStatus.Completed] = [],
        [CrawlerTaskStatus.DeadLettered] = [],
    };

    public static bool CanTransition(CrawlerTaskStatus from, CrawlerTaskStatus to) =>
        Allowed[from].Contains(to);
}
