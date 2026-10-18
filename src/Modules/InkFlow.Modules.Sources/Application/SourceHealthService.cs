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
    private readonly SourceHealthAvailabilityReader availabilityReader =
        new(repository, clock, sourceRepository);
    private readonly SourceHealthMutationWriter mutationWriter =
        new(repository, clock);

    public Task<SourceCapabilityHealth?> GetAsync(
        string sourceId,
        SourceCapability capability,
        CancellationToken cancellationToken = default) =>
        availabilityReader.GetAsync(sourceId, capability, cancellationToken);

    public Task<IReadOnlyList<SourceCapabilityHealth>> ListForSourceAsync(
        string sourceId,
        CancellationToken cancellationToken = default) =>
        availabilityReader.ListForSourceAsync(sourceId, cancellationToken);

    public Task<bool> IsAvailableAsync(
        string sourceId,
        SourceCapability capability,
        CancellationToken cancellationToken = default) =>
        availabilityReader.IsAvailableAsync(sourceId, capability, cancellationToken);

    public Task<SourceCapabilityHealth> RecordSuccessAsync(
        string sourceId,
        SourceCapability capability,
        CancellationToken cancellationToken = default) =>
        mutationWriter.RecordSuccessAsync(sourceId, capability, cancellationToken);

    public Task<SourceCapabilityHealth> RecordFailureAsync(
        string sourceId,
        SourceCapability capability,
        string reason,
        CancellationToken cancellationToken = default) =>
        mutationWriter.RecordFailureAsync(sourceId, capability, reason, cancellationToken);

    public Task<SourceCapabilityHealth> DisableAsync(
        string sourceId,
        SourceCapability capability,
        string reason,
        CancellationToken cancellationToken = default) =>
        mutationWriter.DisableAsync(sourceId, capability, reason, cancellationToken);

    public Task<SourceCapabilityHealth> EnableAsync(
        string sourceId,
        SourceCapability capability,
        CancellationToken cancellationToken = default) =>
        mutationWriter.EnableAsync(sourceId, capability, cancellationToken);
}
