using InkFlow.Modules.Crawling.Domain;

namespace InkFlow.Modules.Crawling.Application;

public sealed record CollectionRunView(
    Guid Id,
    string SourceId,
    string ExternalBookId,
    string InputUrl,
    Guid? CanonicalBookId,
    CollectionRunStatus Status,
    CollectionRunStage Stage,
    int TotalTaskCount,
    int CompletedTaskCount,
    int FailedTaskCount,
    int PendingTaskCount,
    int InFlightTaskCount,
    int CancelledTaskCount,
    int RemainingTaskCount,
    string? LastError,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? BookTitle = null)
{
    public int? ProgressPercent => Stage != CollectionRunStage.Content || TotalTaskCount == 0
        ? null
        : Math.Clamp((int)Math.Round(
            (double)CompletedTaskCount / TotalTaskCount * 100,
            MidpointRounding.AwayFromZero), 0, 100);
}

public sealed record CollectionRunStartOutcome(
    bool IsSuccess,
    CollectionRunView? Run,
    bool Reused,
    string? ErrorCode,
    string? Error)
{
    public static CollectionRunStartOutcome Failure(string code, string error) =>
        new(false, null, false, code, error);
}

public sealed record CollectionRunControlOutcome(
    bool IsSuccess,
    CollectionRunView? Run,
    string? ErrorCode,
    string? Error)
{
    public static CollectionRunControlOutcome Failure(string code, string error) =>
        new(false, null, code, error);
}

public sealed record CollectionRunDeleteOutcome(
    bool IsSuccess,
    string? ErrorCode = null,
    string? Error = null)
{
    public static CollectionRunDeleteOutcome Deleted() => new(true);

    public static CollectionRunDeleteOutcome Failure(string code, string error) =>
        new(false, code, error);
}

public sealed record CollectionRunCleanupOutcome(
    bool IsSuccess,
    int DeletedCount,
    string? ErrorCode = null,
    string? Error = null)
{
    public static CollectionRunCleanupOutcome Cleaned(int deletedCount) =>
        new(true, deletedCount);

    public static CollectionRunCleanupOutcome Failure(string code, string error) =>
        new(false, 0, code, error);
}

/// <summary>
/// 采集运行编排：负责直接地址入口、控制命令、阶段推进和父子进度折叠。
/// 外部请求只创建/控制运行，不同步执行第三方请求。
/// </summary>
public sealed class CollectionRunService
{
    private readonly CollectionRunCommands commands;
    private readonly CollectionRunQueries queries;

    public CollectionRunService(
        SourceBookUrlResolver urlResolver,
        ICollectionRunRepository runs,
        TimeProvider clock)
    {
        queries = new(runs);
        commands = new(urlResolver, runs, clock, queries);
    }

    public Task<CollectionRunStartOutcome> StartFromUrlAsync(
        string? inputUrl,
        CancellationToken cancellationToken = default) =>
        commands.StartFromUrlAsync(inputUrl, cancellationToken);

    public Task<CollectionRunView?> GetViewAsync(
        Guid runId,
        CancellationToken cancellationToken = default) =>
        queries.GetViewAsync(runId, cancellationToken);

    public Task<IReadOnlyList<CollectionRunView>> ListViewsAsync(
        int limit,
        CancellationToken cancellationToken = default) =>
        queries.ListViewsAsync(limit, cancellationToken);

    public Task<CollectionRunViewPage> ListPageViewsAsync(
        int limit,
        CollectionRunCursor? before,
        CancellationToken cancellationToken = default) =>
        queries.ListPageViewsAsync(limit, before, cancellationToken);

    public Task<CollectionRunDeleteOutcome> DeleteFailedAsync(
        Guid runId,
        string? reason,
        CancellationToken cancellationToken = default) =>
        commands.DeleteFailedAsync(runId, reason, cancellationToken);

    public Task<CollectionRunCleanupOutcome> DeleteCancelledAsync(
        string? reason,
        CancellationToken cancellationToken = default) =>
        commands.DeleteCancelledAsync(reason, cancellationToken);

    public Task<CollectionRunControlOutcome> ControlAsync(
        Guid runId,
        string? action,
        string? reason,
        CancellationToken cancellationToken = default) =>
        commands.ControlAsync(runId, action, reason, cancellationToken);

    public Task<bool> CanScheduleFollowUpAsync(
        Guid runId,
        CancellationToken cancellationToken = default) =>
        queries.CanScheduleFollowUpAsync(runId, cancellationToken);

    public Task<CollectionRunStatus?> GetStatusAsync(
        Guid runId,
        CancellationToken cancellationToken = default) =>
        queries.GetStatusAsync(runId, cancellationToken);

    public Task SetCanonicalBookAsync(
        Guid runId,
        Guid canonicalBookId,
        CancellationToken cancellationToken = default) =>
        commands.SetCanonicalBookAsync(runId, canonicalBookId, cancellationToken);

    public Task AdvanceStageAsync(
        Guid runId,
        CollectionRunStage stage,
        CancellationToken cancellationToken = default) =>
        commands.AdvanceStageAsync(runId, stage, cancellationToken);

    public Task MarkWorkStartedAsync(
        Guid runId,
        CancellationToken cancellationToken = default) =>
        commands.MarkWorkStartedAsync(runId, cancellationToken);

    public Task ReconcileAsync(
        Guid runId,
        CancellationToken cancellationToken = default) =>
        commands.ReconcileAsync(runId, cancellationToken);
}
