using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

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

    Task<IReadOnlyList<SourceCapabilityHealth>> ListForSourceAsync(
        string sourceId,
        CancellationToken cancellationToken = default);
}
