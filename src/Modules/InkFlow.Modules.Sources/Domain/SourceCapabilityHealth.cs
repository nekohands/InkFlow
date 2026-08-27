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
///
/// 自适应恢复：Unhealthy 不是终态——冷却期满后允许下一次真实抓取充当探针
/// （半开语义），成功即回 Healthy，失败则按失败深度指数延长下一次冷却。
/// 冷却时长由连续失败次数推导，无需额外持久化字段。
/// </summary>
public static class SourceHealthPolicy
{
    public const string AlgorithmVersion = "source-health-v1";
    public const int UnhealthyAfterConsecutiveFailures = 3;
    public const int MaxFailureReasonLength = 1024;

    /// <summary>首次进入 Unhealthy 后的基础冷却期。</summary>
    public const int ProbeCooldownBaseMinutes = 30;

    /// <summary>探针冷却上限:持续失败的来源最多每天被重试一次。</summary>
    public const int ProbeCooldownMaxMinutes = 24 * 60;

    private const int ProbeCooldownDoublingLimit = 10;

    /// <summary>Unhealthy 来源的探针冷却期:30 分钟起步,每多一次失败翻倍,封顶一天。</summary>
    public static TimeSpan ProbeCooldown(int consecutiveFailures)
    {
        var extra = Math.Max(0, consecutiveFailures - UnhealthyAfterConsecutiveFailures);
        var minutes = (long)ProbeCooldownBaseMinutes << Math.Min(extra, ProbeCooldownDoublingLimit);
        return TimeSpan.FromMinutes(Math.Min(minutes, ProbeCooldownMaxMinutes));
    }

    /// <summary>冷却是否已过:以最后一次状态变化时间为基准,含边界时刻判定为到期。</summary>
    public static bool IsProbeDue(
        int consecutiveFailures,
        DateTimeOffset lastTransitionAt,
        DateTimeOffset now) =>
        now - lastTransitionAt >= ProbeCooldown(consecutiveFailures);

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
        // 计数不再封顶:超出阈值的失败深度驱动探针冷却的指数退避。
        ConsecutiveFailures++;
        Status = ConsecutiveFailures >= SourceHealthPolicy.UnhealthyAfterConsecutiveFailures
            ? SourceHealthStatus.Unhealthy
            : SourceHealthStatus.Degraded;
        LastFailureAt = now;
        LastFailureReason = NormalizeReason(reason);
        UpdatedAt = now;
    }

    /// <summary>
    /// 半开语义:Unhealthy 且冷却期已过的来源应放行下一次真实抓取作为探针;
    /// 探针成败经 RecordSuccess/RecordFailure 上报——失败会刷新时间锚点并按
    /// 增长的失败深度延长冷却。Disabled 永不参与。
    /// </summary>
    public bool IsProbeDue(DateTimeOffset now) =>
        Status == SourceHealthStatus.Unhealthy &&
        SourceHealthPolicy.IsProbeDue(ConsecutiveFailures, UpdatedAt, now);

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
