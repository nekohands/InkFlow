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
/// 来源健康策略的运行时参数快照。v1 的曲线常量来自编译期 const；
/// 引入本 record 后，宿主可在启动时经 <see cref="SourceHealthPolicy.Configure"/>
/// 装载运营配置（冷却曲线等），持久化状态与算法版本不变。
/// </summary>
/// <param name="UnhealthyAfterConsecutiveFailures">连续失败多少次进入 Unhealthy。</param>
/// <param name="ProbeCooldownBaseMinutes">首次 Unhealthy 的基础冷却（分钟）。</param>
/// <param name="ProbeCooldownMaxMinutes">探针冷却上限（分钟）。</param>
public sealed record SourceHealthParameters(
    int UnhealthyAfterConsecutiveFailures,
    int ProbeCooldownBaseMinutes,
    int ProbeCooldownMaxMinutes)
{
    /// <summary>
    /// v1 编译期默认。必须引用常量而非 SourceHealthPolicy 的可变静态属性：
    /// 否则首次访问发生在 Configure 之后时会捕获运行时快照，
    /// 使「传 null 恢复默认」的语义失效。
    /// </summary>
    public static readonly SourceHealthParameters Default = new(
        SourceHealthPolicy.V1UnhealthyAfterConsecutiveFailures,
        SourceHealthPolicy.V1ProbeCooldownBaseMinutes,
        SourceHealthPolicy.V1ProbeCooldownMaxMinutes);

    private const int DoublingLimit = 10;

    /// <summary>
    /// 指数退避冷却：基础时长起步，每超出阈值的失败深度翻倍，封顶上限。
    /// 曲线的唯一实现——静态策略只是当前装载参数的只读视图。
    /// </summary>
    public TimeSpan ProbeCooldown(int consecutiveFailures)
    {
        var extra = Math.Max(0, consecutiveFailures - UnhealthyAfterConsecutiveFailures);
        var minutes = (long)ProbeCooldownBaseMinutes << Math.Min(extra, DoublingLimit);
        return TimeSpan.FromMinutes(Math.Min(minutes, ProbeCooldownMaxMinutes));
    }
}

/// <summary>
/// 来源健康策略 v1：健康按能力独立记录；连续失败达到阈值后才进入 Unhealthy，
/// 避免一次短暂网络抖动就让来源退出候选。算法版本随持久化状态保存。
///
/// 自适应恢复：Unhealthy 不是终态——冷却期满后允许下一次真实抓取充当探针
/// （半开语义），成功即回 Healthy，失败则按失败深度指数延长下一次冷却。
/// 冷却时长由连续失败次数推导，无需额外持久化字段。
///
/// 曲线常量支持运行时覆盖：<see cref="Configure"/> 在组合根装载
/// <see cref="SourceHealthParameters"/>；未配置时全部入口回落 v1 默认值，
/// 既有持久化数据与测试断言不受影响。
/// </summary>
public static class SourceHealthPolicy
{
    public const string AlgorithmVersion = "source-health-v1";

    // v1 编译期默认;静态属性初始值与 SourceHealthParameters.Default 均由此派生。
    internal const int V1UnhealthyAfterConsecutiveFailures = 3;
    internal const int V1ProbeCooldownBaseMinutes = 30;
    internal const int V1ProbeCooldownMaxMinutes = 24 * 60;

    public const int MaxFailureReasonLength = 1024;

    /// <summary>
    /// 当前装载的参数快照（未配置时即 v1 默认）。曲线算法的唯一实现
    /// 在 <see cref="SourceHealthParameters.ProbeCooldown"/>；静态入口
    /// 只是该快照的只读视图。
    /// </summary>
    public static SourceHealthParameters Parameters { get; private set; } =
        SourceHealthParameters.Default;

    /// <summary>
    /// 组合根在宿主启动时装载运营参数；此后所有冷却/阈值读取走配置值。
    /// 传 null 显式恢复 v1 默认。
    /// </summary>
    public static void Configure(SourceHealthParameters? parameters)
    {
        Parameters = parameters ?? SourceHealthParameters.Default;
    }

    /// <summary>v1 默认阈值；运行时经 <see cref="Configure"/> 覆盖后随之变化。</summary>
    public static int UnhealthyAfterConsecutiveFailures =>
        Parameters.UnhealthyAfterConsecutiveFailures;

    /// <summary>v1 默认基础冷却；运行时经 <see cref="Configure"/> 覆盖后随之变化。</summary>
    public static int ProbeCooldownBaseMinutes => Parameters.ProbeCooldownBaseMinutes;

    /// <summary>v1 默认冷却上限；运行时经 <see cref="Configure"/> 覆盖后随之变化。</summary>
    public static int ProbeCooldownMaxMinutes => Parameters.ProbeCooldownMaxMinutes;

    /// <summary>Unhealthy 来源的探针冷却期:基础时长起步,每多一次失败翻倍,封顶上限。</summary>
    public static TimeSpan ProbeCooldown(int consecutiveFailures) =>
        Parameters.ProbeCooldown(consecutiveFailures);

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
