namespace InkFlow.Modules.Sources.Domain;

public enum SourceHealthStatus
{
    Unknown,
    Healthy,
    Degraded,
    Unhealthy,
    Disabled,
}

/// <summary>
/// 来源健康策略 v1：健康按能力独立记录；连续三次运行失败后才进入 Unhealthy，
/// 避免一次短暂网络抖动就让来源退出候选。算法版本随持久化状态保存。
/// </summary>
public static class SourceHealthPolicy
{
    public const string AlgorithmVersion = "source-health-v1";
    public const int UnhealthyAfterConsecutiveFailures = 3;
    public const int MaxFailureReasonLength = 1024;

    public static bool IsAvailable(SourceHealthStatus status) =>
        status is not SourceHealthStatus.Unhealthy and not SourceHealthStatus.Disabled;
}

/// <summary>
/// 单一来源能力的运行健康状态。它是运营事实，不混入 Source 的规则/元数据聚合。
/// </summary>
public sealed class SourceCapabilityHealth
{
    public string SourceId { get; private set; } = null!;
    public SourceCapability Capability { get; private set; }
    public SourceHealthStatus Status { get; private set; }
    public int ConsecutiveFailures { get; private set; }
    public DateTimeOffset? LastSuccessAt { get; private set; }
    public DateTimeOffset? LastFailureAt { get; private set; }
    public string? LastFailureReason { get; private set; }
    public string AlgorithmVersion { get; private set; } = SourceHealthPolicy.AlgorithmVersion;
    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsAvailable => SourceHealthPolicy.IsAvailable(Status);

    private SourceCapabilityHealth() { }

    public static SourceCapabilityHealth Create(
        string sourceId, SourceCapability capability, DateTimeOffset now)
    {
        ValidateSourceId(sourceId);

        return new SourceCapabilityHealth
        {
            SourceId = sourceId,
            Capability = capability,
            Status = SourceHealthStatus.Unknown,
            UpdatedAt = now,
        };
    }

    public static SourceCapabilityHealth Rehydrate(
        string sourceId,
        SourceCapability capability,
        SourceHealthStatus status,
        int consecutiveFailures,
        DateTimeOffset? lastSuccessAt,
        DateTimeOffset? lastFailureAt,
        string? lastFailureReason,
        string? algorithmVersion,
        DateTimeOffset updatedAt)
    {
        ValidateSourceId(sourceId);
        if (consecutiveFailures < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(consecutiveFailures));
        }

        return new SourceCapabilityHealth
        {
            SourceId = sourceId,
            Capability = capability,
            Status = status,
            ConsecutiveFailures = consecutiveFailures,
            LastSuccessAt = lastSuccessAt,
            LastFailureAt = lastFailureAt,
            LastFailureReason = NormalizeReason(lastFailureReason),
            AlgorithmVersion = string.IsNullOrWhiteSpace(algorithmVersion)
                ? SourceHealthPolicy.AlgorithmVersion
                : algorithmVersion,
            UpdatedAt = updatedAt,
        };
    }

    public void RecordSuccess(DateTimeOffset now)
    {
        Status = SourceHealthStatus.Healthy;
        ConsecutiveFailures = 0;
        LastSuccessAt = now;
        LastFailureReason = null;
        UpdatedAt = now;
    }

    public void RecordFailure(string reason, DateTimeOffset now)
    {
        ConsecutiveFailures = Math.Min(
            ConsecutiveFailures + 1,
            SourceHealthPolicy.UnhealthyAfterConsecutiveFailures);
        Status = ConsecutiveFailures >= SourceHealthPolicy.UnhealthyAfterConsecutiveFailures
            ? SourceHealthStatus.Unhealthy
            : SourceHealthStatus.Degraded;
        LastFailureAt = now;
        LastFailureReason = NormalizeReason(reason);
        UpdatedAt = now;
    }

    /// <summary>运营侧主动禁用某一能力；保留原因，恢复时可审计。</summary>
    public void Disable(string reason, DateTimeOffset now)
    {
        Status = SourceHealthStatus.Disabled;
        LastFailureReason = NormalizeReason(reason);
        UpdatedAt = now;
    }

    /// <summary>恢复后回到 Unknown，等待下一次真实探测确认 Healthy。</summary>
    public void Enable(DateTimeOffset now)
    {
        Status = SourceHealthStatus.Unknown;
        ConsecutiveFailures = 0;
        LastFailureReason = null;
        UpdatedAt = now;
    }

    private static void ValidateSourceId(string sourceId)
    {
        if (string.IsNullOrWhiteSpace(sourceId) || sourceId.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException(
                "source id must be non-empty without whitespace.", nameof(sourceId));
        }
    }

    private static string? NormalizeReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return null;
        }

        var trimmed = reason.Trim();
        return trimmed.Length <= SourceHealthPolicy.MaxFailureReasonLength
            ? trimmed
            : trimmed[..SourceHealthPolicy.MaxFailureReasonLength];
    }
}
