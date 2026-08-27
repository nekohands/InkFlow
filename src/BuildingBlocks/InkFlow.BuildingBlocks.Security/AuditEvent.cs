namespace InkFlow.BuildingBlocks.Security;

/// <summary>
/// 不可变的审计事实。高风险命令可使用 Reference 保存脱敏后的变更引用，
/// 不应把 token、Cookie、正文或其他秘密状态直接写入事件。
/// </summary>
public sealed record AuditEvent
{
    public Guid Id { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
    public string ActorType { get; init; } = null!;
    public string? ActorId { get; init; }
    public string Action { get; init; } = null!;
    public string Resource { get; init; } = null!;
    public string Outcome { get; init; } = null!;
    public int StatusCode { get; init; }
    public string? Reason { get; init; }
    public string? TraceId { get; init; }
    public string? Reference { get; init; }

    public static AuditEvent Create(
        string action,
        string resource,
        string outcome,
        int statusCode,
        DateTimeOffset occurredAt,
        string actorType = "anonymous",
        string? actorId = null,
        string? reason = null,
        string? traceId = null,
        string? reference = null)
    {
        if (statusCode is < 100 or > 599)
        {
            throw new ArgumentOutOfRangeException(nameof(statusCode));
        }

        return new AuditEvent
        {
            Id = Guid.NewGuid(),
            OccurredAt = occurredAt,
            ActorType = NormalizeRequired(actorType, nameof(actorType), 64),
            ActorId = NormalizeOptional(actorId, 256),
            Action = NormalizeRequired(action, nameof(action), 128),
            Resource = NormalizeRequired(resource, nameof(resource), 512),
            Outcome = NormalizeRequired(outcome, nameof(outcome), 64),
            StatusCode = statusCode,
            Reason = NormalizeOptional(reason, 512),
            TraceId = NormalizeOptional(traceId, 128),
            Reference = NormalizeOptional(reference, 512),
        };
    }

    private static string NormalizeRequired(string value, string parameterName, int maxLength)
    {
        var normalized = NormalizeOptional(value, maxLength);
        return normalized ?? throw new ArgumentException(
            "audit field must not be empty.", parameterName);
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim()
            .Replace('\r', ' ')
            .Replace('\n', ' ');
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength];
    }
}

/// <summary>审计事件的追加写入端口；实现可以是日志、数据库或外部不可变存储。</summary>
public interface IAuditEventSink
{
    ValueTask AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);
}
