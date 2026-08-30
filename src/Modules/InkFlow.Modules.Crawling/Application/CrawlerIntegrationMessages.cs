using System.Text.Json;
using System.Text.Json.Serialization;
using InkFlow.BuildingBlocks.Messaging;
using InkFlow.Modules.Crawling.Domain;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Crawling.Application;

/// <summary>
/// Crawler 对外发布的最小稳定事实。变量和凭据引用不进入事件载荷，
/// 下游如需业务详情必须通过受控的模块契约读取。
/// </summary>
public static class CrawlerIntegrationMessages
{
    public const string TaskCreatedType = "crawler.task.created";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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

    /// <summary>
    /// 读取并校验任务创建事件的稳定字段。变量和凭据不在事件契约内，
    /// 接收方必须回到 CrawlerTask 权威事实读取完整任务。
    /// </summary>
    public static CrawlerTaskCreatedMessage ReadTaskCreated(IntegrationMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!string.Equals(message.MessageType, TaskCreatedType, StringComparison.Ordinal))
        {
            throw new ArgumentException("integration message type is not crawler.task.created.", nameof(message));
        }

        TaskCreatedPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<TaskCreatedPayload>(message.Payload, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "crawler.task.created payload is invalid.",
                exception);
        }

        if (payload is null ||
            payload.TaskId == Guid.Empty ||
            payload.SourceId is null ||
            string.IsNullOrWhiteSpace(payload.SourceId) ||
            payload.Capability is null ||
            payload.Status is null ||
            payload.AttemptCount < 0 ||
            payload.CreatedAt == DateTimeOffset.MinValue ||
            payload.CreatedAt == DateTimeOffset.MaxValue)
        {
            throw new InvalidOperationException("crawler.task.created payload is incomplete.");
        }

        if (message.Id != payload.TaskId)
        {
            throw new InvalidOperationException("crawler.task.created message identity is invalid.");
        }

        if (!Enum.TryParse<SourceCapability>(payload.Capability, ignoreCase: false, out var capability) ||
            !Enum.IsDefined(capability))
        {
            throw new InvalidOperationException("crawler.task.created capability is invalid.");
        }

        if (!Enum.TryParse<CrawlerTaskStatus>(payload.Status, ignoreCase: false, out var status) ||
            !Enum.IsDefined(status))
        {
            throw new InvalidOperationException("crawler.task.created status is invalid.");
        }

        return new(
            payload.TaskId,
            payload.SourceId,
            capability,
            status,
            payload.AttemptCount,
            payload.CreatedAt);
    }

    private sealed record TaskCreatedPayload(
        [property: JsonPropertyName("taskId")] Guid TaskId,
        [property: JsonPropertyName("sourceId")] string? SourceId,
        [property: JsonPropertyName("capability")] string? Capability,
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("attemptCount")] int AttemptCount,
        [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt);
}

public sealed record CrawlerTaskCreatedMessage(
    Guid TaskId,
    string SourceId,
    SourceCapability Capability,
    CrawlerTaskStatus Status,
    int AttemptCount,
    DateTimeOffset CreatedAt);
