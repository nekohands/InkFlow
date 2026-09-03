using InkFlow.Modules.Crawling.Domain;
using InkFlow.Modules.Sources.Domain;

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

/// <summary>
/// 采集运行编排：负责直接地址入口、控制命令、阶段推进和父子进度折叠。
/// 外部请求只创建/控制运行，不同步执行第三方请求。
/// </summary>
public sealed class CollectionRunService(
    SourceBookUrlResolver urlResolver,
    ICollectionRunRepository runs,
    TimeProvider clock)
{
    private const int MaxControlReasonLength = 512;

    public async Task<CollectionRunStartOutcome> StartFromUrlAsync(
        string? inputUrl,
        CancellationToken cancellationToken = default)
    {
        var resolved = await urlResolver
            .ResolveAsync(inputUrl, cancellationToken)
            .ConfigureAwait(false);
        if (!resolved.IsSuccess)
        {
            return CollectionRunStartOutcome.Failure(
                resolved.ErrorCode ?? "source-url.unresolved",
                resolved.Error ?? "book URL could not be resolved.");
        }

        var existing = await runs
            .FindActiveAsync(
                resolved.SourceId!,
                resolved.ExternalBookId!,
                cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return new(
                true,
                await BuildViewAsync(existing, cancellationToken).ConfigureAwait(false),
                true,
                null,
                null);
        }

        var now = clock.GetUtcNow();
        var run = CollectionRun.Create(
            resolved.SourceId!,
            resolved.ExternalBookId!,
            resolved.NormalizedUrl!,
            now);
        var initialTask = CrawlerTask.Create(
            new CrawlPayload(
                run.SourceId,
                SourceCapability.BookInfo,
                new Dictionary<string, string>
                {
                    ["bookId"] = run.ExternalBookId,
                    ["reason"] = "direct-url",
                },
                RunId: run.Id),
            createdAt: now);
        if (!await runs
                .TryAddWithInitialTaskAsync(run, initialTask, cancellationToken)
                .ConfigureAwait(false))
        {
            var concurrent = await runs
                .FindActiveAsync(
                    resolved.SourceId!,
                    resolved.ExternalBookId!,
                    cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException(
                    "an active collection run disappeared during concurrent creation.");
            return new(
                true,
                await BuildViewAsync(concurrent, cancellationToken).ConfigureAwait(false),
                true,
                null,
                null);
        }

        await ReconcileAsync(run.Id, cancellationToken).ConfigureAwait(false);
        var created = await GetViewAsync(run.Id, cancellationToken).ConfigureAwait(false);
        return new(true, created, false, null, null);
    }

    public async Task<CollectionRunView?> GetViewAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        var run = await runs.GetAsync(runId, cancellationToken).ConfigureAwait(false);
        return run is null ? null : await BuildViewAsync(run, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CollectionRunView>> ListViewsAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        var page = await ListPageViewsAsync(limit, null, cancellationToken).ConfigureAwait(false);
        return page.Entries;
    }

    public async Task<CollectionRunViewPage> ListPageViewsAsync(
        int limit,
        CollectionRunCursor? before,
        CancellationToken cancellationToken = default)
    {
        var page = await runs
            .ListPageAsync(limit, before, cancellationToken)
            .ConfigureAwait(false);
        var views = new List<CollectionRunView>(page.Entries.Count);
        foreach (var run in page.Entries)
        {
            views.Add(await BuildViewAsync(run, cancellationToken).ConfigureAwait(false));
        }

        return new(views, page.NextCursor);
    }

    public async Task<CollectionRunDeleteOutcome> DeleteFailedAsync(
        Guid runId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var normalizedReason = reason?.Trim() ?? string.Empty;
        if (normalizedReason.Length == 0 || normalizedReason.Length > MaxControlReasonLength)
        {
            return CollectionRunDeleteOutcome.Failure(
                "collection-run.reason",
                "a deletion reason between 1 and 512 characters is required.");
        }

        var deleted = await runs.DeleteFailedAsync(runId, cancellationToken).ConfigureAwait(false);
        return deleted switch
        {
            true => CollectionRunDeleteOutcome.Deleted(),
            false => CollectionRunDeleteOutcome.Failure(
                "collection-run.not-failed",
                "only failed collection runs can be deleted."),
            null => CollectionRunDeleteOutcome.Failure(
                "collection-run.not-found",
                "collection run was not found."),
        };
    }

    public async Task<CollectionRunControlOutcome> ControlAsync(
        Guid runId,
        string? action,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var normalizedAction = action?.Trim().ToLowerInvariant();
        if (normalizedAction is not ("pause" or "resume" or "stop" or "cancel"))
        {
            return CollectionRunControlOutcome.Failure(
                "collection-run.action", "action must be pause, resume, stop, or cancel.");
        }

        var normalizedReason = reason?.Trim() ?? string.Empty;
        if (normalizedReason.Length == 0 || normalizedReason.Length > MaxControlReasonLength)
        {
            return CollectionRunControlOutcome.Failure(
                "collection-run.reason", "a control reason between 1 and 512 characters is required.");
        }

        try
        {
            var run = await runs
                .ApplyControlAsync(
                    runId,
                    normalizedAction,
                    clock.GetUtcNow(),
                    cancellationToken)
                .ConfigureAwait(false);
            if (run is null)
            {
                return CollectionRunControlOutcome.Failure(
                    "collection-run.not-found", "collection run was not found.");
            }

            if (normalizedAction is "stop" or "cancel" or "resume")
            {
                await ReconcileAsync(run.Id, cancellationToken).ConfigureAwait(false);
            }

            var view = await GetViewAsync(run.Id, cancellationToken).ConfigureAwait(false);
            return new(true, view, null, null);
        }
        catch (InvalidOperationException)
        {
            return CollectionRunControlOutcome.Failure(
                "collection-run.invalid-state",
                "collection run cannot apply this control in its current state.");
        }
    }

    public async Task<bool> CanScheduleFollowUpAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        var run = await runs.GetAsync(runId, cancellationToken).ConfigureAwait(false);
        return run?.CanScheduleFollowUp == true;
    }

    public async Task<CollectionRunStatus?> GetStatusAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        var run = await runs.GetAsync(runId, cancellationToken).ConfigureAwait(false);
        return run?.Status;
    }

    public async Task SetCanonicalBookAsync(
        Guid runId,
        Guid canonicalBookId,
        CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        await MutateRunAsync(
                runId,
                run => run.SetCanonicalBook(canonicalBookId, now),
                now,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AdvanceStageAsync(
        Guid runId,
        CollectionRunStage stage,
        CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        await MutateRunAsync(
                runId,
                run => run.AdvanceTo(stage, now),
                now,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task MarkWorkStartedAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        await MutateRunAsync(
                runId,
                run => run.MarkWorkStarted(now),
                now,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task ReconcileAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        await runs
            .ReconcileAsync(runId, clock.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task MutateRunAsync(
        Guid runId,
        Action<CollectionRun> mutation,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (await runs
                .MutateAsync(runId, mutation, now, cancellationToken)
                .ConfigureAwait(false) is null)
        {
            throw new InvalidOperationException($"collection run {runId} was not found.");
        }
    }

    private async Task<CollectionRunView> BuildViewAsync(
        CollectionRun run,
        CancellationToken cancellationToken)
    {
        var progress = await runs
            .GetTaskProgressAsync(run.Id, cancellationToken)
            .ConfigureAwait(false);
        return new(
            run.Id,
            run.SourceId,
            run.ExternalBookId,
            run.InputUrl,
            run.CanonicalBookId,
            run.Status,
            run.Stage,
            progress.TotalTaskCount,
            progress.CompletedTaskCount,
            progress.DeadLetteredTaskCount,
            progress.PendingTaskCount,
            progress.InFlightTaskCount,
            progress.CancelledTaskCount,
            progress.RemainingTaskCount,
            run.LastError,
            run.CreatedAt,
            run.UpdatedAt);
    }
}
