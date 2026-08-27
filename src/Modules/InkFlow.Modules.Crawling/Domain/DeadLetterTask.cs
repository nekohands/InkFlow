namespace InkFlow.Modules.Crawling.Domain;

/// <summary>重试耗尽后的失败记录。修复流程以它为入口，不允许静默丢弃失败任务。</summary>
public sealed record DeadLetterTask(
    Guid Id,
    Guid TaskId,
    string SourceId,
    string Reason,
    int AttemptCount,
    DateTimeOffset DeadLetteredAt,
    Guid? ReplayTaskId = null,
    DateTimeOffset? ReplayedAt = null,
    string? ReplayRequestedBy = null,
    string? ReplayReason = null)
{
    public bool IsReplayed => ReplayTaskId is not null;

    public static DeadLetterTask From(CrawlerTask task, string reason, DateTimeOffset now)
    {
        if (task.Status != CrawlerTaskStatus.DeadLettered)
        {
            throw new InvalidOperationException(
                $"cannot dead-letter a task in status {task.Status}.");
        }

        return new DeadLetterTask(Guid.NewGuid(), task.Id, task.Payload.SourceId, reason, task.AttemptCount, now);
    }
}
