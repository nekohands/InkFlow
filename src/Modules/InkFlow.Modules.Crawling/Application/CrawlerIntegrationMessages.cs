using System.Text.Json;
using InkFlow.BuildingBlocks.Messaging;
using InkFlow.Modules.Crawling.Domain;

namespace InkFlow.Modules.Crawling.Application;

/// <summary>
/// Crawler 对外发布的最小稳定事实。变量和凭据引用不进入事件载荷，
/// 下游如需业务详情必须通过受控的模块契约读取。
/// </summary>
public static class CrawlerIntegrationMessages
{
    public const string TaskCreatedType = "crawler.task.created";

    public static IntegrationMessage TaskCreated(
        CrawlerTask task,
        string? traceId = null)
    {
        ArgumentNullException.ThrowIfNull(task);

        var payload = JsonSerializer.Serialize(new
        {
            taskId = task.Id,
            sourceId = task.Payload.SourceId,
            capability = task.Payload.Capability.ToString(),
            status = task.Status.ToString(),
            attemptCount = task.AttemptCount,
            createdAt = task.CreatedAt,
        });

        return IntegrationMessage.Create(
            TaskCreatedType,
            payload,
            task.CreatedAt,
            traceId,
            task.Id);
    }
}
