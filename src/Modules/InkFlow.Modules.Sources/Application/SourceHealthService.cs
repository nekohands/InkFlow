using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

/// <summary>
/// 来源能力健康深模块：统一状态转移、阈值策略、手动禁用/恢复和可用性读取。
/// 调用方不需要知道健康表结构或连续失败规则。
/// </summary>
public sealed class SourceHealthService(
    ISourceHealthRepository repository,
    TimeProvider clock,
    ISourceRepository? sourceRepository = null) : ISourceHealthReader, ISourceHealthRecorder, ISourceHealthOperations
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
            await sourceRepository.GetAsync(sourceId, cancellationToken).ConfigureAwait(false)
                is { IsEnabled: false })
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

    public Task<SourceCapabilityHealth> RecordSuccessAsync(
        string sourceId,
        SourceCapability capability,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            sourceId,
            capability,
            SourceHealthMutationKind.RecordSuccess,
            reason: null,
            cancellationToken);

    public Task<SourceCapabilityHealth> RecordFailureAsync(
        string sourceId,
        SourceCapability capability,
        string reason,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            sourceId,
            capability,
            SourceHealthMutationKind.RecordFailure,
            reason,
            cancellationToken);

    public Task<SourceCapabilityHealth> DisableAsync(
        string sourceId,
        SourceCapability capability,
        string reason,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            sourceId,
            capability,
            SourceHealthMutationKind.Disable,
            reason,
            cancellationToken);

    public Task<SourceCapabilityHealth> EnableAsync(
        string sourceId,
        SourceCapability capability,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            sourceId,
            capability,
            SourceHealthMutationKind.Enable,
            reason: null,
            cancellationToken);

    private Task<SourceCapabilityHealth> MutateAsync(
        string sourceId,
        SourceCapability capability,
        SourceHealthMutationKind mutation,
        string? reason,
        CancellationToken cancellationToken)
        => repository.MutateAsync(
            sourceId,
            capability,
            mutation,
            reason,
            clock.GetUtcNow(),
            cancellationToken);
}
