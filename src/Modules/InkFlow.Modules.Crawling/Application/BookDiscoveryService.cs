using InkFlow.Modules.Library.Application;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Crawling.Application;

/// <summary>一个搜索命中的归并形态:同一正典书被多个来源命中时合为一条。</summary>
public sealed record DiscoveredBook(
    Guid CanonicalBookId,
    string Title,
    string Author,
    IReadOnlyList<string> SourceIds,
    bool AlreadyInLibrary);

/// <summary>
/// 来源搜索发现的编排结果。Warnings 保留逐源失败阶段的稳定提示,保证发现过程可解释，
/// 同时不把底层异常细节返回给调用方。
/// </summary>
public sealed record DiscoveryOutcome(
    IReadOnlyList<DiscoveredBook> Books,
    IReadOnlyList<string> Warnings)
{
    public static DiscoveryOutcome Empty() => new([], []);
}

/// <summary>
/// 搜索发现编排:对每个已登记且 Search 能力健康的来源执行关键词搜索,
/// 命中后幂等导入来源书目(BookInfo upsert)并走 v1 匹配
/// (Confirmed 幂等 / 同名同作者挂接既有正典书 / 新建),最后按正典书归并。
/// 失败隔离:单来源异常只产生 warning,不影响其他来源的命中。
/// 目录/正文的后续更新由 Scheduler → Worker 追更链路自动接管,
/// 阅读路径仍零实时抓取(架构不变量 3)。
/// </summary>
public sealed class BookDiscoveryService(
    ISourceRepository sources,
    ISourceAdapterFactory adapterFactory,
    SourceCatalogService catalog,
    CanonicalBookMatchingService matching,
    ISourceHealthReader? healthReader = null)
{
    public async Task<DiscoveryOutcome> DiscoverAsync(
        string query, CancellationToken cancellationToken = default)
    {
        var keyword = query?.Trim() ?? string.Empty;
        if (keyword.Length == 0)
        {
            return DiscoveryOutcome.Empty();
        }

        var allSources = await sources.ListAsync(cancellationToken).ConfigureAwait(false);
        var warnings = new List<string>();
        var byCanonical = new Dictionary<Guid, DiscoveredBook>();
        var newlyCreated = new HashSet<Guid>();

        foreach (var source in allSources)
        {
            try
            {
                if (!source.IsEnabled)
                {
                    warnings.Add($"search: source '{source.Id}' skipped (source disabled).");
                    continue;
                }

                if (healthReader is not null && !await healthReader
                        .IsAvailableAsync(source.Id, SourceCapability.Search, cancellationToken)
                        .ConfigureAwait(false))
                {
                    warnings.Add($"search: source '{source.Id}' skipped (Search capability unavailable).");
                    continue;
                }

                var adapter = await adapterFactory
                    .GetAdapterAsync(source.Id, cancellationToken)
                    .ConfigureAwait(false);
                if (adapter is null)
                {
                    warnings.Add($"search: source '{source.Id}' skipped (no usable adapter).");
                    continue;
                }

                IReadOnlyList<SourceSearchResult> hits;
                try
                {
                    hits = await adapter.SearchAsync(keyword, cancellationToken).ConfigureAwait(false);
                }
                catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    warnings.Add(CreateFailureWarning("search", source.Id));
                    continue;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    warnings.Add(CreateFailureWarning("search", source.Id));
                    continue;
                }

                foreach (var hit in hits)
                {
                    var import = await catalog
                        .ImportBookInfoAsync(source.Id, hit.ExternalBookId, cancellationToken)
                        .ConfigureAwait(false);

                    if (!import.IsSuccess)
                    {
                        warnings.Add($"import: source '{source.Id}' book '{hit.ExternalBookId}': " +
                                     string.Join("; ", import.Errors));
                        continue;
                    }

                    var match = await matching
                        .CreateOrMatchAsync(source.Id, hit.ExternalBookId, cancellationToken)
                        .ConfigureAwait(false);

                    if (!match.IsSuccess || match.Book is null)
                    {
                        warnings.Add($"match: source '{source.Id}' book '{hit.ExternalBookId}': " +
                                     string.Join("; ", match.Errors));
                        continue;
                    }

                    if (match.NewlyCreated)
                    {
                        newlyCreated.Add(match.Book.Id);
                    }

                    if (byCanonical.TryGetValue(match.Book.Id, out var existing))
                    {
                        if (!existing.SourceIds.Contains(source.Id))
                        {
                            byCanonical[match.Book.Id] = existing with
                            {
                                SourceIds = [.. existing.SourceIds, source.Id],
                            };
                        }
                    }
                    else
                    {
                        byCanonical[match.Book.Id] = new DiscoveredBook(
                            match.Book.Id,
                            match.Book.Title,
                            match.Book.Author,
                            [source.Id],
                            AlreadyInLibrary: !newlyCreated.Contains(match.Book.Id));
                    }
                }
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                warnings.Add(CreateFailureWarning("discovery", source.Id));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Import/match/factory/health failures remain isolated to this source.
                warnings.Add(CreateFailureWarning("discovery", source.Id));
            }
        }

        var books = byCanonical.Values
            .OrderBy(b => b.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new DiscoveryOutcome(books, warnings);
    }

    private static string CreateFailureWarning(string phase, string sourceId) =>
        $"{phase}: source '{sourceId}' failed; retry later.";
}
