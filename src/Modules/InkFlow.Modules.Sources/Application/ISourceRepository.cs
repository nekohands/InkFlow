using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

/// <summary>来源仓储契约。</summary>
public interface ISourceRepository
{
    Task AddAsync(Source source, CancellationToken cancellationToken = default);

    Task<Source?> GetAsync(string sourceId, CancellationToken cancellationToken = default);

    /// <summary>全部已登记来源(含规则文档),供搜索发现等需要枚举来源的编排使用。</summary>
    Task<IReadOnlyList<Source>> ListAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(Source source, CancellationToken cancellationToken = default);
}
