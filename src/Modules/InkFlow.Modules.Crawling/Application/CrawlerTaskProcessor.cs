using InkFlow.BuildingBlocks.Observability;
using InkFlow.Modules.Crawling.Domain;
using Microsoft.Extensions.DependencyInjection;

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
    private readonly CrawlerTaskRunCoordinator runCoordinator =
        new(collectionRuns);
    private readonly CrawlerTaskExecutionRunner executionRunner =
        new(executor, tasks, clock, leaseDuration, scopeFactory);
    private readonly CrawlerTaskFailureHandler failureHandler =
        new(tasks, clock, retryPolicy, failureReporter);

    public async Task ProcessAsync(
        CrawlerTask task,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);

        try
        {
            if (await runCoordinator
                    .ShouldCancelBeforeExecutionAsync(task, cancellationToken)
                    .ConfigureAwait(false))
            {
                await CancelTaskAsync(task, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (!await tasks
                    .TryMarkRunningAsync(task, clock.GetUtcNow(), cancellationToken)
                    .ConfigureAwait(false))
            {
                await runCoordinator.ReconcileAsync(task, cancellationToken).ConfigureAwait(false);
                return;
            }

            // The repository gate is authoritative for production persistence:
            // it rechecks the parent run and atomically starts the task. Keep
            // the service mutation only as a compatibility fallback for older
            // in-memory repositories, and never advance a run before the task
            // start has been accepted.
            await runCoordinator
                .MarkWorkStartedAsync(task, cancellationToken)
                .ConfigureAwait(false);

            var outcome = await executionRunner.RunAsync(task, cancellationToken)
                .ConfigureAwait(false);
            if (outcome is null)
            {
                return;
            }

            var runStatus = await runCoordinator
                .GetStatusAsync(task, cancellationToken)
                .ConfigureAwait(false);
            if (CrawlerTaskRunCoordinator.IsTerminal(runStatus))
            {
                await CancelTaskAsync(task, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (runStatus == CollectionRunStatus.Stopping)
            {
                if (outcome.Succeeded)
                {
                    await CompleteTaskAsync(task, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await CancelTaskAsync(task, cancellationToken).ConfigureAwait(false);
                }

                await runCoordinator.ReconcileAsync(task, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (outcome.Succeeded)
            {
                await CompleteTaskAsync(task, cancellationToken).ConfigureAwait(false);
                await runCoordinator.ReconcileAsync(task, cancellationToken).ConfigureAwait(false);
                return;
            }

            await failureHandler
                .HandleAsync(
                    task,
                    outcome.FailureReason ?? "unknown",
                    cancellationToken)
                .ConfigureAwait(false);
            await runCoordinator.ReconcileAsync(task, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            if (await runCoordinator
                    .ShouldCancelAfterFailureAsync(task, cancellationToken)
                    .ConfigureAwait(false))
            {
                await CancelTaskAsync(task, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await failureHandler
                    .HandleAsync(task, "crawler task execution failed.", cancellationToken)
                    .ConfigureAwait(false);
            }

            await runCoordinator
                .ReconcileAsync(task, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task CompleteTaskAsync(
        CrawlerTask task,
        CancellationToken cancellationToken)
    {
        task.Complete(clock.GetUtcNow());
        await tasks.SaveAsync(task, cancellationToken).ConfigureAwait(false);
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

}
