using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

public sealed record SourceHealthScanCursor(
    string SourceId,
    SourceCapability Capability);

public sealed record SourceHealthPage(
    IReadOnlyList<SourceCapabilityHealth> Health,
    SourceHealthScanCursor? NextCursor,
    bool HasMore);

/// <summary>来源能力健康的权威存储契约；Redis/缓存不得替代它。</summary>
public interface ISourceHealthRepository
{
    Task<SourceCapabilityHealth?> GetAsync(
        string sourceId,
        SourceCapability capability,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        SourceCapabilityHealth health,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        SourceCapabilityHealth health,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 在权威存储内串行读取、执行一次领域变更并保存；实现必须按来源能力复合键保证原子性。
    /// </summary>
    Task<SourceCapabilityHealth> MutateAsync(
        string sourceId,
        SourceCapability capability,
        SourceHealthMutationKind mutation,
        string? reason,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SourceCapabilityHealth>> ListForSourceAsync(
        string sourceId,
        CancellationToken cancellationToken = default);

    /// <summary>全部处于 Unhealthy 状态的能力行;主动巡检按此候选冷却期判定。</summary>
    Task<IReadOnlyList<SourceCapabilityHealth>> ListUnhealthyAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 读取稳定排序的 Unhealthy 候选页;默认实现仅供非生产替身兼容,生产仓储必须下推分页。
    /// </summary>
    async Task<SourceHealthPage> ListUnhealthyPageAsync(
        SourceHealthScanCursor? after,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        var ordered = (await ListUnhealthyAsync(cancellationToken).ConfigureAwait(false))
            .Where(health => after is null
                || string.CompareOrdinal(health.SourceId, after.SourceId) > 0
                || (health.SourceId == after.SourceId && health.Capability > after.Capability))
            .OrderBy(health => health.SourceId)
            .ThenBy(health => health.Capability)
            .Take(limit + 1)
            .ToList();

        var hasMore = ordered.Count > limit;
        if (hasMore)
        {
            ordered.RemoveAt(limit);
        }

        var nextCursor = hasMore
            ? new SourceHealthScanCursor(ordered[^1].SourceId, ordered[^1].Capability)
            : null;
        return new SourceHealthPage(ordered, nextCursor, hasMore);
    }
}
