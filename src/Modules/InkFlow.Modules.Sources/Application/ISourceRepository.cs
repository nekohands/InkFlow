using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

/// <summary>来源仓储契约。</summary>
public interface ISourceRepository
{
    Task AddAsync(Source source, CancellationToken cancellationToken = default);

    Task<Source?> GetAsync(string sourceId, CancellationToken cancellationToken = default);

    Task SaveAsync(Source source, CancellationToken cancellationToken = default);
}
