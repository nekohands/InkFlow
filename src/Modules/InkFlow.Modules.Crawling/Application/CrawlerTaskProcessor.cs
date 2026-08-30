using InkFlow.BuildingBlocks.Observability;
using InkFlow.Modules.Crawling.Domain;

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
    CrawlerFailureReporter failureReporter) : ICrawlerTaskProcessor
{
    public async Task ProcessAsync(
        CrawlerTask task,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);

        try
        {
            task.MarkRunning(clock.GetUtcNow());
            await tasks.SaveAsync(task, cancellationToken).ConfigureAwait(false);

            var outcome = await executor
                .ExecuteAsync(task, cancellationToken)
                .ConfigureAwait(false);
            if (outcome.Succeeded)
            {
                task.Complete(clock.GetUtcNow());
                await tasks.SaveAsync(task, cancellationToken).ConfigureAwait(false);
                return;
            }

            await FailTaskAsync(
                    task,
                    outcome.FailureReason ?? "unknown",
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await FailTaskAsync(task, exception.Message, cancellationToken)
                .ConfigureAwait(false);
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
            await tasks
                .AddDeadLetterAsync(
                    DeadLetterTask.From(task, reason, now),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await tasks.SaveAsync(task, cancellationToken).ConfigureAwait(false);
    }
}
