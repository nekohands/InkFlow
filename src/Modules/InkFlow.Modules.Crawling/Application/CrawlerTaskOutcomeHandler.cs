using InkFlow.Modules.Crawling.Domain;

namespace InkFlow.Modules.Crawling.Application;

/// <summary>
/// 统一执行结果到任务事实及父级 CollectionRun 的收敛策略。
/// </summary>
internal sealed class CrawlerTaskOutcomeHandler(
    ICrawlerTaskRepository tasks,
    TimeProvider clock,
    CrawlerTaskRunCoordinator runCoordinator,
    CrawlerTaskFailureHandler failureHandler)
{
    public async Task HandleAsync(
        CrawlerTask task,
        CrawlOutcome outcome,
        CancellationToken cancellationToken)
    {
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
        }
        else
        {
            await failureHandler
                .HandleAsync(
                    task,
                    outcome.FailureReason ?? "unknown",
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await runCoordinator.ReconcileAsync(task, cancellationToken).ConfigureAwait(false);
    }

    public async Task HandleExceptionAsync(
        CrawlerTask task,
        CancellationToken cancellationToken)
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

        await runCoordinator.ReconcileAsync(task, cancellationToken).ConfigureAwait(false);
    }

    public Task CancelAsync(
        CrawlerTask task,
        CancellationToken cancellationToken) =>
        CancelTaskAsync(task, cancellationToken);

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
