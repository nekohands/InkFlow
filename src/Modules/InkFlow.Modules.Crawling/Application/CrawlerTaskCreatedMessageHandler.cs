using InkFlow.BuildingBlocks.Messaging;
using InkFlow.Modules.Crawling.Domain;

namespace InkFlow.Modules.Crawling.Application;

public static class CrawlerTaskExecutionDefaults
{
    public const string Owner = "inkflow-worker";

    public static TimeSpan LeaseDuration => TimeSpan.FromMinutes(2);
}

/// <summary>
/// Crawler 任务创建事件的业务接收者。
/// 完整任务仍从 CrawlerTask 权威仓储读取；消息只负责触发一次按 ID 的原子领取。
/// Inbox 确认与任务状态提交是两个独立事务，重复投递通过终态/租约和任务 ID 幂等吸收。
/// </summary>
public sealed class CrawlerTaskCreatedMessageHandler(
    ICrawlerTaskRepository tasks,
    ICrawlerTaskProcessor processor,
    TimeProvider clock) : IIntegrationMessageHandler
{
    public string MessageType => CrawlerIntegrationMessages.TaskCreatedType;

    public async Task HandleAsync(
        IntegrationMessage message,
        CancellationToken cancellationToken = default)
    {
        var created = CrawlerIntegrationMessages.ReadTaskCreated(message);
        var task = await tasks
            .GetAsync(created.TaskId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("crawler task created event references a missing task.");

        if (!string.Equals(task.Payload.SourceId, created.SourceId, StringComparison.Ordinal) ||
            task.Payload.Capability != created.Capability ||
            task.CreatedAt != created.CreatedAt)
        {
            throw new InvalidOperationException(
                "crawler.task.created does not match the authoritative task.");
        }

        // 事件可能在任务已被周期轮询、成功完成或人工重放后才到达；这些情况都不应再次执行。
        if (task.Status is CrawlerTaskStatus.Completed or CrawlerTaskStatus.DeadLettered)
        {
            return;
        }

        var leased = await tasks
            .TryLeaseAsync(
                created.TaskId,
                clock.GetUtcNow().ToUniversalTime(),
                CrawlerTaskExecutionDefaults.Owner,
                CrawlerTaskExecutionDefaults.LeaseDuration,
                cancellationToken)
            .ConfigureAwait(false);

        // 已由轮询入口领取、尚未到 ScheduledAt 或刚被其他进程处理时，
        // 任务表和周期轮询仍是可靠兜底；本事件不强行绕过任务租约。
        if (leased is null)
        {
            return;
        }

        await processor
            .ProcessAsync(leased, cancellationToken)
            .ConfigureAwait(false);
    }
}
