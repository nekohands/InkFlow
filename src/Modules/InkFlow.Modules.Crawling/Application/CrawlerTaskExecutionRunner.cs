using InkFlow.Modules.Crawling.Domain;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.ExceptionServices;

namespace InkFlow.Modules.Crawling.Application;

internal sealed class CrawlerTaskExecutionRunner(
    ICrawlerTaskExecutor executor,
    ICrawlerTaskRepository tasks,
    TimeProvider clock,
    TimeSpan? leaseDuration = null,
    IServiceScopeFactory? scopeFactory = null)
{
    public async Task<CrawlOutcome?> RunAsync(
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
        var heartbeat = new CrawlerLeaseHeartbeat(tasks, clock, scopeFactory)
            .RenewUntilStoppedAsync(
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
}
