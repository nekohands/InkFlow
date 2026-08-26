using InkFlow.Modules.Crawling.Domain;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Crawling.Application;

/// <summary>
/// 抓取任务的持久化仓储。实现负责聚合与实体之间的映射，
/// 以及租约领取查询的并发正确性（同一任务不能同时被两个 worker 领取）。
/// </summary>
public interface ICrawlerTaskRepository
{
    Task AddAsync(CrawlerTask task, CancellationToken cancellationToken = default);

    Task<CrawlerTask?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>把聚合的当前状态写回存储。</summary>
    Task SaveAsync(CrawlerTask task, CancellationToken cancellationToken = default);

    /// <summary>
    /// 找出在 <paramref name="now"/> 时刻可领取（Pending，或租约已过期）的任务。
    /// 实现必须保证同一批结果内不含重复任务；跨 worker 的互斥由租约语义 + 存储层约束共同保证。
    /// </summary>
    Task<IReadOnlyList<CrawlerTask>> FindLeasableAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken = default);

    Task AddDeadLetterAsync(DeadLetterTask deadLetter, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DeadLetterTask>> ListDeadLettersAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>是否存在某来源某能力的活跃(Pending/Leased/Running)任务,用于入队去重。</summary>
    Task<bool> HasActiveTaskAsync(
        string sourceId, SourceCapability capability, CancellationToken cancellationToken = default);
}
