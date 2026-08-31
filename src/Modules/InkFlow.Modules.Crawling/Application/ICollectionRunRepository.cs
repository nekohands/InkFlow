using InkFlow.Modules.Crawling.Domain;

namespace InkFlow.Modules.Crawling.Application;

/// <summary>采集运行及其子任务进度的持久化契约。</summary>
public interface ICollectionRunRepository
{
    Task AddAsync(CollectionRun run, CancellationToken cancellationToken = default);

    /// <summary>
    /// 尝试插入活跃运行；持久化实现必须把同来源/外部书籍的并发竞争收敛为一次成功插入。
    /// 返回 false 表示已有活跃运行，调用方随后读取该运行复用。
    /// </summary>
    Task<bool> TryAddAsync(
        CollectionRun run,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 在同一数据库事务中插入运行、首个采集任务及其 TaskCreated Outbox 消息。
    /// 返回 false 表示已有活跃运行；其他持久化失败必须向调用方抛出，不能留下无任务运行。
    /// </summary>
    Task<bool> TryAddWithInitialTaskAsync(
        CollectionRun run,
        CrawlerTask task,
        CancellationToken cancellationToken = default);

    Task<CollectionRun?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 在持久化层的同一原子操作中读取、应用控制状态并保存运行。
    /// EF 实现使用行锁；默认实现保持内存仓储/旧测试替身兼容。
    /// </summary>
    Task<CollectionRun?> ApplyControlAsync(
        Guid id,
        string action,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        return ApplyControlFallbackAsync(id, action, now, cancellationToken);
    }

    /// <summary>
    /// 在持久化层的同一原子操作中读取运行、汇总子任务并折叠进度。
    /// 实现必须避免陈旧 Reconcile 快照覆写并发控制命令。
    /// </summary>
    Task<CollectionRun?> ReconcileAsync(
        Guid id,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        return ReconcileFallbackAsync(id, now, cancellationToken);
    }

    /// <summary>
    /// 在持久化层的同一原子操作中锁定并修改运行聚合。
    /// 实现必须避免陈旧聚合快照覆写并发控制命令。
    /// </summary>
    Task<CollectionRun?> MutateAsync(
        Guid id,
        Action<CollectionRun> mutation,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        return MutateFallbackAsync(id, mutation, now, cancellationToken);
    }

    Task<CollectionRun?> FindActiveAsync(
        string sourceId,
        string externalBookId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CollectionRun>> ListAsync(
        int limit,
        CancellationToken cancellationToken = default);

    Task SaveAsync(CollectionRun run, CancellationToken cancellationToken = default);

    Task<CollectionRunTaskProgress> GetTaskProgressAsync(
        Guid runId,
        CancellationToken cancellationToken = default);

    private async Task<CollectionRun?> ApplyControlFallbackAsync(
        Guid id,
        string action,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var run = await GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (run is null)
        {
            return null;
        }

        switch (action)
        {
            case "pause":
                run.Pause(now);
                break;
            case "resume":
                run.Resume(now);
                break;
            case "stop":
                run.RequestStop(now);
                break;
            case "cancel":
                run.Cancel(now);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action));
        }

        await SaveAsync(run, cancellationToken).ConfigureAwait(false);
        return run;
    }

    private async Task<CollectionRun?> ReconcileFallbackAsync(
        Guid id,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var run = await GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (run is null)
        {
            return null;
        }

        var progress = await GetTaskProgressAsync(id, cancellationToken).ConfigureAwait(false);
        run.Reconcile(progress, now);
        await SaveAsync(run, cancellationToken).ConfigureAwait(false);
        return run;
    }

    private async Task<CollectionRun?> MutateFallbackAsync(
        Guid id,
        Action<CollectionRun> mutation,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mutation);

        var run = await GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (run is null)
        {
            return null;
        }

        mutation(run);
        await SaveAsync(run, cancellationToken).ConfigureAwait(false);
        return run;
    }
}
