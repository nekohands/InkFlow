using InkFlow.BuildingBlocks.Observability;
using InkFlow.Modules.Crawling.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace InkFlow.Modules.Crawling.Application;

/// <summary>
/// 任务执行的唯一编排模块：把已领取任务推进到 Running，执行能力处理器，
/// 再将成功、可重试失败或死信写回 CrawlerTask 权威事实。
/// </summary>
public sealed class CrawlerTaskProcessor : ICrawlerTaskProcessor
{
    private readonly ICrawlerTaskRepository tasks;
    private readonly TimeProvider clock;
    private readonly CrawlerTaskRunCoordinator runCoordinator;
    private readonly CrawlerTaskExecutionRunner executionRunner;
    private readonly CrawlerTaskFailureHandler failureHandler;
    private readonly CrawlerTaskOutcomeHandler outcomeHandler;

    public CrawlerTaskProcessor(
        ICrawlerTaskExecutor executor,
        ICrawlerTaskRepository tasks,
        TimeProvider clock,
        RetryPolicy retryPolicy,
        CrawlerFailureReporter failureReporter,
        CollectionRunService? collectionRuns = null,
        TimeSpan? leaseDuration = null,
        IServiceScopeFactory? scopeFactory = null)
    {
        this.tasks = tasks;
        this.clock = clock;
        runCoordinator = new(collectionRuns);
        executionRunner = new(executor, tasks, clock, leaseDuration, scopeFactory);
        failureHandler = new(tasks, clock, retryPolicy, failureReporter);
        outcomeHandler = new(tasks, clock, runCoordinator, failureHandler);
    }

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
                await outcomeHandler.CancelAsync(task, cancellationToken).ConfigureAwait(false);
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

            await outcomeHandler
                .HandleAsync(task, outcome, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            await outcomeHandler
                .HandleExceptionAsync(task, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
