using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

public sealed record SourceScanCursor(string Id);

public sealed record SourcePage(
    IReadOnlyList<Source> Sources,
    SourceScanCursor? NextCursor,
    bool HasMore);

/// <summary>来源仓储契约。</summary>
public interface ISourceRepository
{
    Task AddAsync(Source source, CancellationToken cancellationToken = default);

    Task<Source?> GetAsync(string sourceId, CancellationToken cancellationToken = default);

    /// <summary>只读取来源是否存在及启用状态，不加载 Rule DSL。</summary>
    async Task<bool?> GetEnabledAsync(
        string sourceId,
        CancellationToken cancellationToken = default)
    {
        var source = await GetAsync(sourceId, cancellationToken).ConfigureAwait(false);
        return source?.IsEnabled;
    }

    /// <summary>全部已登记来源(含规则文档),供显式全量快照调用方使用。</summary>
    Task<IReadOnlyList<Source>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>按 Source.Id 稳定 keyset 分页读取来源；默认实现兼容旧的内存替身。</summary>
    Task<SourcePage> ListPageAsync(
        SourceScanCursor? after,
        int limit,
        CancellationToken cancellationToken = default) =>
        ListPageFallbackAsync(after, limit, cancellationToken);

    Task SaveAsync(Source source, CancellationToken cancellationToken = default);

    private async Task<SourcePage> ListPageFallbackAsync(
        SourceScanCursor? after,
        int limit,
        CancellationToken cancellationToken)
    {
        if (limit is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        var entries = (await ListAsync(cancellationToken).ConfigureAwait(false))
            .OrderBy(source => source.Id, StringComparer.Ordinal)
            .Where(source => after is null ||
                string.Compare(source.Id, after.Id, StringComparison.Ordinal) > 0)
            .Take(limit + 1)
            .ToList();
        var hasMore = entries.Count > limit;
        if (hasMore)
        {
            entries.RemoveAt(limit);
        }

        return new SourcePage(
            entries,
            hasMore ? new SourceScanCursor(entries[^1].Id) : null,
            hasMore);
    }
}
