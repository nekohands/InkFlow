using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Crawling.Application;

internal sealed record BookDiscoverySourceRead(
    IReadOnlyList<SourceSearchResult> Hits,
    IReadOnlyList<string> Warnings);

/// <summary>
/// 发现流程的来源读取边界：筛选可用来源、解析适配器、执行 Search 并把单来源故障
/// 转换为稳定 warning。导入、匹配和正典归并仍由 BookDiscoveryService 编排。
/// </summary>
internal sealed class BookDiscoverySourceReader(
    ISourceAdapterFactory adapterFactory,
    ISourceHealthReader? healthReader)
{
    public async Task<BookDiscoverySourceRead> ReadAsync(
        SourcePageEntry source,
        string keyword,
        int maxHits,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!source.IsEnabled)
            {
                return new([], [$"search: source '{source.Id}' skipped (source disabled)."]);
            }

            if (healthReader is not null && !await healthReader
                    .IsAvailableAsync(source.Id, SourceCapability.Search, cancellationToken)
                    .ConfigureAwait(false))
            {
                return new([], [
                    $"search: source '{source.Id}' skipped (Search capability unavailable)."]);
            }

            var adapter = await adapterFactory
                .GetAdapterAsync(source.Id, cancellationToken)
                .ConfigureAwait(false);
            if (adapter is null)
            {
                return new([], [$"search: source '{source.Id}' skipped (no usable adapter)."]);
            }

            IReadOnlyList<SourceSearchResult> hits;
            try
            {
                hits = await adapter.SearchAsync(keyword, cancellationToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return FailedSearch(source.Id);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return FailedSearch(source.Id);
            }

            if (hits.Count <= maxHits)
            {
                return new(hits, []);
            }

            return new(
                hits.Take(maxHits).ToArray(),
                [$"search: source '{source.Id}' results truncated at {maxHits}."]);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return FailedDiscovery(source.Id);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return FailedDiscovery(source.Id);
        }
    }

    private static BookDiscoverySourceRead FailedSearch(string sourceId) =>
        new([], [$"search: source '{sourceId}' failed; retry later."]);

    private static BookDiscoverySourceRead FailedDiscovery(string sourceId) =>
        new([], [$"discovery: source '{sourceId}' failed; retry later."]);
}
