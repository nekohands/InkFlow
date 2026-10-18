using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

/// <summary>把运行时和运维健康操作收敛到仓储的原子变更 seam。</summary>
internal sealed class SourceHealthMutationWriter(
    ISourceHealthRepository repository,
    TimeProvider clock)
{
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
        CancellationToken cancellationToken) =>
        repository.MutateAsync(
            sourceId,
            capability,
            mutation,
            reason,
            clock.GetUtcNow(),
            cancellationToken);
}
