using InkFlow.BuildingBlocks.Observability;
using InkFlow.Modules.Crawling.Domain;

namespace InkFlow.Modules.Crawling.Application;

/// <summary>任务失败后的重试、死信原子写入和结构化观测策略。</summary>
internal sealed class CrawlerTaskFailureHandler(
    ICrawlerTaskRepository tasks,
    TimeProvider clock,
    RetryPolicy retryPolicy,
    CrawlerFailureReporter failureReporter)
{
    public async Task HandleAsync(
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
