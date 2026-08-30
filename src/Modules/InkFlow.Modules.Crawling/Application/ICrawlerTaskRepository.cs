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

    /// <summary>
    /// 在存储层原子领取一个任务。实现必须在数据库锁范围内完成候选筛选与租约写入，
    /// 以保证多个 Worker 进程不会领取同一个任务。
    /// </summary>
    Task<CrawlerTask?> TryLeaseAsync(
        DateTimeOffset now,
        string owner,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 在存储层原子领取指定任务。任务事件只能通过此入口触发执行，
    /// 不得先读取再在内存中写入租约，以免与周期轮询并发重复执行。
    /// </summary>
    Task<CrawlerTask?> TryLeaseAsync(
        Guid taskId,
        DateTimeOffset now,
        string owner,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    /// <summary>把聚合的当前状态写回存储。</summary>
    Task SaveAsync(CrawlerTask task, CancellationToken cancellationToken = default);

    /// <summary>
    /// 找出在 <paramref name="now"/> 时刻可领取（已到 <c>ScheduledAt</c> 的 Pending，
    /// 或 Leased/Running 租约已过期）的任务。
    /// 该方法用于候选发现；需要真正领取时必须调用 <see cref="TryLeaseAsync"/>。
    /// </summary>
    Task<IReadOnlyList<CrawlerTask>> FindLeasableAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken = default);

    Task AddDeadLetterAsync(DeadLetterTask deadLetter, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DeadLetterTask>> ListDeadLettersAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>是否存在某来源某能力的活跃(Pending/Leased/Running)任务,用于入队去重。</summary>
    Task<bool> HasActiveTaskAsync(
        string sourceId, SourceCapability capability, CancellationToken cancellationToken = default);

    /// <summary>
    /// 是否存在同来源、同能力、且指定变量取值相同的"阻止性"任务
    /// (Pending / Leased / Running / DeadLettered)。Completed 不算冲突。
    /// 死信算冲突:防止周期扫描把已放弃的任务反复复活,死信只能走人工处理路径。
    /// </summary>
    Task<bool> HasConflictingTaskAsync(
        string sourceId,
        SourceCapability capability,
        string variableName,
        string variableValue,
        CancellationToken cancellationToken = default);
}
