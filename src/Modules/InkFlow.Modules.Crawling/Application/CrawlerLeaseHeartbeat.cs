using InkFlow.Modules.Crawling.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace InkFlow.Modules.Crawling.Application;

/// <summary>
/// Keeps a claimed crawler task alive while its executor is running.
/// Losing the lease cancels the executor and leaves terminal state handling to
/// <see cref="CrawlerTaskProcessor"/>.
/// </summary>
internal sealed class CrawlerLeaseHeartbeat(
    ICrawlerTaskRepository tasks,
    TimeProvider clock,
    IServiceScopeFactory? scopeFactory)
{
    public async Task<bool> RenewUntilStoppedAsync(
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
}
