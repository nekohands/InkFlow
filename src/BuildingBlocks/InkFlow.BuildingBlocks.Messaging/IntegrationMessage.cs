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
        ValidatePayload(payload);
        var normalizedTraceId = NormalizeOptionalTraceId(traceId);
        var payloadHash = ComputePayloadHash(payload);
        var messageId = NormalizeId(id ?? Guid.CreateVersion7(), nameof(id));

        return new IntegrationMessage(
            messageId,
            normalizedType,
            occurredAt.ToUniversalTime(),
            payload,
            payloadHash,
            normalizedTraceId);
    }

    /// <summary>
    /// 从持久化记录恢复 Envelope。持久化层可能把 JSON 存为 jsonb，调用方必须传入
    /// 已保存的 hash；此方法校验结构和 hash 格式，但不把规范化后的 JSON 再当作原文重算。
    /// </summary>
    public static IntegrationMessage Restore(
        string messageType,
        string payload,
        DateTimeOffset occurredAt,
        string payloadHash,
        string? traceId,
        Guid id)
    {
        var created = Create(messageType, payload, occurredAt, traceId, id);
        ValidatePayloadHash(payloadHash);
        return new IntegrationMessage(
            created.Id,
            created.MessageType,
            created.OccurredAt,
            created.Payload,
            payloadHash,
            created.TraceId);
    }

    private static void ValidatePayload(string payload)
    {
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
    }

    private static string? NormalizeOptionalTraceId(string? traceId) =>
        string.IsNullOrWhiteSpace(traceId)
            ? null
            : NormalizeRequired(traceId, nameof(traceId), MaxTraceIdLength);

    private static Guid NormalizeId(Guid id, string parameterName)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("message ID must not be empty.", parameterName);
        }

        return id;
    }

    private static string ComputePayloadHash(string payload) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)))
            .ToLowerInvariant();

    private static void ValidatePayloadHash(string payloadHash)
    {
        if (string.IsNullOrWhiteSpace(payloadHash) ||
            payloadHash.Length != 64 ||
            payloadHash.Any(character => !IsLowerHex(character)))
        {
            throw new ArgumentException(
                "persisted payload hash must be lowercase SHA-256 hex.",
                nameof(payloadHash));
        }
    }

    private static bool IsLowerHex(char character) =>
        character is >= '0' and <= '9' or >= 'a' and <= 'f';

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
