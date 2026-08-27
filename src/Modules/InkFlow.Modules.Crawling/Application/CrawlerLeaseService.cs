using InkFlow.Modules.Crawling.Domain;

namespace InkFlow.Modules.Crawling.Application;

/// <summary>
/// 领域层租约辅助服务：实现单个聚合内的租约状态流转。
/// 持久化任务的跨进程互斥由 ICrawlerTaskRepository 的数据库事务/行锁领取路径保证。
/// </summary>
public sealed class CrawlerLeaseService(TimeProvider clock)
{
    public TimeSpan DefaultLeaseDuration { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>尝试领取任务。成功返回 true 并写入租约；失败返回 false（不修改状态）。</summary>
    public bool TryLease(CrawlerTask task, string owner)
    {
        var now = clock.GetUtcNow();
        if ((task.Status is CrawlerTaskStatus.Leased or CrawlerTaskStatus.Running) &&
            task.LeaseExpiresAt is { } expiry && expiry <= now)
        {
            task.ReleaseExpiredLease(now);
        }

        if (!task.IsLeasable(now))
        {
            return false;
        }

        task.Lease(owner, now, DefaultLeaseDuration);
        return true;
    }

    /// <summary>回收所有过期租约，返回被回收的任务。</summary>
    public IReadOnlyList<CrawlerTask> ReleaseExpired(IEnumerable<CrawlerTask> candidates)
    {
        var now = clock.GetUtcNow();
        var released = new List<CrawlerTask>();

        foreach (var task in candidates)
        {
            if ((task.Status is CrawlerTaskStatus.Leased or CrawlerTaskStatus.Running) &&
                task.LeaseExpiresAt is { } expiry && expiry <= now)
            {
                task.ReleaseExpiredLease(now);
                released.Add(task);
            }
        }

        return released;
    }
}
