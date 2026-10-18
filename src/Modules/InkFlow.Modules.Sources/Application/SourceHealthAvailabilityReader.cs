using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

/// <summary>读取健康状态并裁定来源能力是否可作为候选。</summary>
internal sealed class SourceHealthAvailabilityReader(
    ISourceHealthRepository repository,
    TimeProvider clock,
    ISourceRepository? sourceRepository)
{
    public Task<SourceCapabilityHealth?> GetAsync(
        string sourceId,
        SourceCapability capability,
        CancellationToken cancellationToken = default) =>
        repository.GetAsync(sourceId, capability, cancellationToken);

    public Task<IReadOnlyList<SourceCapabilityHealth>> ListForSourceAsync(
        string sourceId,
        CancellationToken cancellationToken = default) =>
        repository.ListForSourceAsync(sourceId, cancellationToken);

    public async Task<bool> IsAvailableAsync(
        string sourceId,
        SourceCapability capability,
        CancellationToken cancellationToken = default)
    {
        if (sourceRepository is not null &&
            await sourceRepository.GetEnabledAsync(sourceId, cancellationToken)
                .ConfigureAwait(false) is false)
        {
            return false;
        }

        var health = await repository
            .GetAsync(sourceId, capability, cancellationToken)
            .ConfigureAwait(false);

        // 没有探测记录的新来源默认可用，首次真实结果负责建立状态。
        if (health is null)
        {
            return true;
        }

        // 半开语义:Unhealthy 且冷却期满的来源放行下一次真实抓取作为探针。
        // 周期扫描(追更扫描/搜索发现)天然充当探测驱动;成败由 Record* 上报——
        // 失败按失败深度指数延长冷却,成功即回 Healthy。
        return health.Status switch
        {
            SourceHealthStatus.Disabled => false,
            SourceHealthStatus.Unhealthy => health.IsProbeDue(clock.GetUtcNow()),
            _ => true,
        };
    }
}
