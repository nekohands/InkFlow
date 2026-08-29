using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace InkFlow.BuildingBlocks.Messaging;

/// <summary>
/// 跨进程传递的稳定集成消息。消息载荷必须是有界、可解析的 JSON；
/// 事件类型和载荷摘要用于消费幂等与篡改检测，不把业务实体直接暴露给消息基础设施。
/// </summary>
public sealed record IntegrationMessage
{
    public const int MaxMessageTypeLength = 128;
    public const int MaxTraceIdLength = 128;
    public const int MaxPayloadBytes = 1024 * 1024;

    private IntegrationMessage(
        Guid id,
        string messageType,
        DateTimeOffset occurredAt,
        string payload,
        string payloadHash,
        string? traceId)
    {
        Id = id;
        MessageType = messageType;
        OccurredAt = occurredAt;
        Payload = payload;
        PayloadHash = payloadHash;
        TraceId = traceId;
    }

    public Guid Id { get; }

    public string MessageType { get; }

    public DateTimeOffset OccurredAt { get; }

    public string Payload { get; }

    public string PayloadHash { get; }

    public string? TraceId { get; }

    public static IntegrationMessage Create(
        string messageType,
        string payload,
        DateTimeOffset occurredAt,
        string? traceId = null,
        Guid? id = null)
    {
        var normalizedType = NormalizeRequired(
            messageType,
            nameof(messageType),
            MaxMessageTypeLength);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        if (Encoding.UTF8.GetByteCount(payload) > MaxPayloadBytes)
        {
            throw new ArgumentException(
                $"message payload must not exceed {MaxPayloadBytes} UTF-8 bytes.",
                nameof(payload));
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
        }
        catch (JsonException exception)
        {
            throw new ArgumentException(
                "message payload must be valid JSON.",
                nameof(payload),
                exception);
        }

        var normalizedTraceId = string.IsNullOrWhiteSpace(traceId)
            ? null
            : NormalizeRequired(traceId, nameof(traceId), MaxTraceIdLength);
        var payloadHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(payload)))
            .ToLowerInvariant();

        var messageId = id ?? Guid.CreateVersion7();
        if (messageId == Guid.Empty)
        {
            throw new ArgumentException("message ID must not be empty.", nameof(id));
        }

        return new IntegrationMessage(
            messageId,
            normalizedType,
            occurredAt.ToUniversalTime(),
            payload,
            payloadHash,
            normalizedTraceId);
    }

    private static string NormalizeRequired(string value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("message value must not be empty.", name);
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength || normalized.Any(char.IsControl))
        {
            throw new ArgumentException("message value is too long or contains control characters.", name);
        }

        return normalized;
    }
}
