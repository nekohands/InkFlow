using InkFlow.BuildingBlocks.Observability;
using InkFlow.Modules.Crawling.Domain;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.ExceptionServices;

namespace InkFlow.Modules.Crawling.Application;

/// <summary>
/// 任务执行的唯一编排模块：把已领取任务推进到 Running，执行能力处理器，
/// 再将成功、可重试失败或死信写回 CrawlerTask 权威事实。
/// </summary>
public sealed class CrawlerTaskProcessor(
    ICrawlerTaskExecutor executor,
    ICrawlerTaskRepository tasks,
    TimeProvider clock,
    RetryPolicy retryPolicy,
    CrawlerFailureReporter failureReporter,
    CollectionRunService? collectionRuns = null,
    TimeSpan? leaseDuration = null,
    IServiceScopeFactory? scopeFactory = null) : ICrawlerTaskProcessor
{
    public async Task ProcessAsync(
        CrawlerTask task,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);

        try
        {
            if (await ShouldCancelTaskAsync(task, cancellationToken).ConfigureAwait(false))
            {
                await CancelTaskAsync(task, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (!await tasks
                    .TryMarkRunningAsync(task, clock.GetUtcNow(), cancellationToken)
                    .ConfigureAwait(false))
            {
                await ReconcileRunAsync(task, cancellationToken).ConfigureAwait(false);
                return;
            }

            // The repository gate is authoritative for production persistence:
            // it rechecks the parent run and atomically starts the task. Keep
            // the service mutation only as a compatibility fallback for older
            // in-memory repositories, and never advance a run before the task
            // start has been accepted.
            if (task.Payload.RunId is { } runId && collectionRuns is not null)
            {
                await collectionRuns.MarkWorkStartedAsync(runId, cancellationToken).ConfigureAwait(false);
            }

            var outcome = await ExecuteWithLeaseHeartbeatAsync(task, cancellationToken)
                .ConfigureAwait(false);
            if (outcome is null)
            {
                return;
            }

            var runStatus = await GetRunStatusAsync(task, cancellationToken).ConfigureAwait(false);
            if (runStatus is CollectionRunStatus.Cancelled or
                CollectionRunStatus.Failed or
                CollectionRunStatus.Stopped or
                CollectionRunStatus.Completed)
            {
                await CancelTaskAsync(task, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (runStatus == CollectionRunStatus.Stopping)
            {
                if (outcome.Succeeded)
                {
                    task.Complete(clock.GetUtcNow());
                    await tasks.SaveAsync(task, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await CancelTaskAsync(task, cancellationToken).ConfigureAwait(false);
                }

                await ReconcileRunAsync(task, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (outcome.Succeeded)
            {
                task.Complete(clock.GetUtcNow());
                await tasks.SaveAsync(task, cancellationToken).ConfigureAwait(false);
                await ReconcileRunAsync(task, cancellationToken).ConfigureAwait(false);
                return;
            }

            await FailTaskAsync(
                    task,
                    outcome.FailureReason ?? "unknown",
                    cancellationToken)
                .ConfigureAwait(false);
            await ReconcileRunAsync(task, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            if (await ShouldCancelAfterFailureAsync(task, cancellationToken).ConfigureAwait(false))
            {
                await CancelTaskAsync(task, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await FailTaskAsync(task, "crawler task execution failed.", cancellationToken)
                    .ConfigureAwait(false);
            }

            await ReconcileRunAsync(task, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task<CrawlOutcome?> ExecuteWithLeaseHeartbeatAsync(
        CrawlerTask task,
        CancellationToken cancellationToken)
    {
        var duration = leaseDuration ?? CrawlerTaskExecutionDefaults.LeaseDuration;
        var interval = TimeSpan.FromTicks(Math.Max(TimeSpan.TicksPerMillisecond, duration.Ticks / 2));
        using var stopHeartbeat = new CancellationTokenSource();
        using var executionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var heartbeatCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            stopHeartbeat.Token);
        var heartbeat = RenewLeaseUntilStoppedAsync(
            task,
            interval,
            heartbeatCancellation.Token,
            executionCancellation,
            duration);

        CrawlOutcome? outcome = null;
        Exception? executionException = null;
        try
        {
            try
            {
                outcome = await executor
                    .ExecuteAsync(task, executionCancellation.Token)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                executionException = exception;
            }
        }
        finally
        {
            stopHeartbeat.Cancel();
        }

        var leaseHeld = await heartbeat.ConfigureAwait(false);
        if (!leaseHeld ||
            (executionException is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return null;
        }

        if (executionException is not null)
        {
            ExceptionDispatchInfo.Capture(executionException).Throw();
        }

        return outcome;
    }

    private async Task<bool> RenewLeaseUntilStoppedAsync(
        CrawlerTask task,
        TimeSpan interval,
        CancellationToken cancellationToken,
        CancellationTokenSource executionCancellation,
        TimeSpan duration)
    {
        try
        {
            using var timer = new PeriodicTimer(interval);
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                if (!await TryRenewLeaseAsync(task, duration, cancellationToken).ConfigureAwait(false))
                {
                    executionCancellation.Cancel();
                    return false;
                }
            }

            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return true;
        }
        catch (Exception)
        {
            executionCancellation.Cancel();
            return false;
        }
    }

    private async Task<bool> TryRenewLeaseAsync(
        CrawlerTask task,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        if (scopeFactory is null)
        {
            return await tasks
                .TryRenewLeaseAsync(
                    task,
                    clock.GetUtcNow().ToUniversalTime(),
                    duration,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICrawlerTaskRepository>();
        return await repository
            .TryRenewLeaseAsync(
                task,
                clock.GetUtcNow().ToUniversalTime(),
                duration,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<bool> ShouldCancelTaskAsync(
        CrawlerTask task,
        CancellationToken cancellationToken)
    {
        var status = await GetRunStatusAsync(task, cancellationToken).ConfigureAwait(false);
        return status is CollectionRunStatus.Cancelled or
            CollectionRunStatus.Failed or
            CollectionRunStatus.Stopped or
            CollectionRunStatus.Completed;
    }

    private async Task<bool> ShouldCancelAfterFailureAsync(
        CrawlerTask task,
        CancellationToken cancellationToken)
    {
        var status = await GetRunStatusAsync(task, cancellationToken).ConfigureAwait(false);
        return status is CollectionRunStatus.Stopping or
            CollectionRunStatus.Cancelled or
            CollectionRunStatus.Failed or
            CollectionRunStatus.Stopped or
            CollectionRunStatus.Completed;
    }

    private async Task<CollectionRunStatus?> GetRunStatusAsync(
        CrawlerTask task,
        CancellationToken cancellationToken)
    {
        return task.Payload.RunId is { } runId && collectionRuns is not null
            ? await collectionRuns.GetStatusAsync(runId, cancellationToken).ConfigureAwait(false)
            : null;
    }

    private async Task CancelTaskAsync(
        CrawlerTask task,
        CancellationToken cancellationToken)
    {
        if (task.Status is not (CrawlerTaskStatus.Completed or
            CrawlerTaskStatus.DeadLettered or
            CrawlerTaskStatus.Cancelled))
        {
            task.Cancel(clock.GetUtcNow());
            await tasks.SaveAsync(task, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ReconcileRunAsync(
        CrawlerTask task,
        CancellationToken cancellationToken)
    {
        if (task.Payload.RunId is { } runId && collectionRuns is not null)
        {
            await collectionRuns.ReconcileAsync(runId, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task FailTaskAsync(
        CrawlerTask task,
        string reason,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        if (task.Status != CrawlerTaskStatus.Running)
        {
            failureReporter.Report(CrawlerFailureObservation.Create(
                task.Id,
                task.Payload.SourceId,
                task.Payload.Capability.ToString(),
                task.AttemptCount,
                task.MaxAttempts,
                CrawlerFailureDisposition.NotRunning,
                reason,
                now));
            return;
        }

        DateTimeOffset? nextAttemptAt = task.AttemptCount < task.MaxAttempts
            ? now + retryPolicy.DelayFor(task.AttemptCount)
            : null;
        task.Fail(now, nextAttemptAt);
        failureReporter.Report(CrawlerFailureObservation.Create(
            task.Id,
            task.Payload.SourceId,
            task.Payload.Capability.ToString(),
            task.AttemptCount,
            task.MaxAttempts,
            task.Status == CrawlerTaskStatus.DeadLettered
                ? CrawlerFailureDisposition.DeadLetter
                : CrawlerFailureDisposition.Retry,
            reason,
            now));

        if (task.Status == CrawlerTaskStatus.DeadLettered)
        {
            // 死信行与任务终态必须原子落库：分两次提交时，任一写失败都会留下
            // 半一致状态（有死信无终态 → 修复视图与任务状态漂移；有终态无死信 → 无法重放）。
            await tasks
                .AddDeadLetterWithTaskAsync(
                    DeadLetterTask.From(task, reason, now),
                    task,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await tasks.SaveAsync(task, cancellationToken).ConfigureAwait(false);
    }
}
