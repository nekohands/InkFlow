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
/// 来源搜索发现的编排结果。Warnings 保留逐源失败阶段和结果截断的稳定提示,保证发现过程可解释，
/// 同时不把底层异常细节返回给调用方。
/// </summary>
public sealed record DiscoveryOutcome(
    IReadOnlyList<DiscoveredBook> Books,
    IReadOnlyList<string> Warnings)
{
    public static DiscoveryOutcome Empty() => new([], []);
}

/// <summary>
/// 搜索发现编排:对每个已登记且 Search 能力健康的来源执行有界关键词搜索,
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
    private readonly BookDiscoverySourceReader sourceReader =
        new(adapterFactory, healthReader);
    private readonly BookDiscoveryHitProcessor hitProcessor =
        new(catalog, matching);

    public const int MaxQueryLength = 256;
    public const int MaxSourceHits = 100;
    public const int MaxDiscoveredBooks = 100;
    public const int MaxSourcesPerPage = 100;

    public async Task<DiscoveryOutcome> DiscoverAsync(
        string query, CancellationToken cancellationToken = default)
    {
        var keyword = query?.Trim() ?? string.Empty;
        if (keyword.Length == 0)
        {
            return DiscoveryOutcome.Empty();
        }

        if (keyword.Length > MaxQueryLength)
        {
            return new DiscoveryOutcome([], ["search: query exceeds maximum length."]);
        }

        var warnings = new List<string>();
        var byCanonical = new Dictionary<Guid, DiscoveredBook>();
        var newlyCreated = new HashSet<Guid>();
        var totalTruncated = false;
        SourceScanCursor? after = null;

        while (true)
        {
            var page = await sources
                .ListPageAsync(after, MaxSourcesPerPage, cancellationToken)
                .ConfigureAwait(false);

            foreach (var source in page.Sources)
            {
                if (byCanonical.Count >= MaxDiscoveredBooks)
                {
                    warnings.Add($"search: results truncated at {MaxDiscoveredBooks}.");
                    totalTruncated = true;
                    break;
                }

                try
                {
                    var read = await sourceReader
                        .ReadAsync(source, keyword, MaxSourceHits, cancellationToken)
                        .ConfigureAwait(false);
                    warnings.AddRange(read.Warnings);

                    foreach (var hit in read.Hits)
                    {
                        if (byCanonical.Count >= MaxDiscoveredBooks)
                        {
                            warnings.Add($"search: results truncated at {MaxDiscoveredBooks}.");
                            totalTruncated = true;
                            break;
                        }

                        var processed = await hitProcessor
                            .ProcessAsync(source.Id, hit, cancellationToken)
                            .ConfigureAwait(false);
                        warnings.AddRange(processed.Warnings);

                        if (!processed.IsSuccess)
                        {
                            continue;
                        }

                        if (processed.NewlyCreated)
                        {
                            newlyCreated.Add(processed.Book!.Id);
                        }

                        if (byCanonical.TryGetValue(processed.Book!.Id, out var existing))
                        {
                            if (!existing.SourceIds.Contains(source.Id))
                            {
                                byCanonical[processed.Book.Id] = existing with
                                {
                                    SourceIds = [.. existing.SourceIds, source.Id],
                                };
                            }
                        }
                        else
                        {
                            byCanonical[processed.Book.Id] = new DiscoveredBook(
                                processed.Book.Id,
                                processed.Book.Title,
                                processed.Book.Author,
                                [source.Id],
                                AlreadyInLibrary: !newlyCreated.Contains(processed.Book.Id));
                        }
                    }

                    if (totalTruncated)
                    {
                        break;
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

            if (totalTruncated || !page.HasMore)
            {
                break;
            }

            after = page.NextCursor ?? throw new InvalidOperationException(
                "source page reported more entries without a continuation cursor.");
        }

        var books = byCanonical.Values
            .OrderBy(b => b.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new DiscoveryOutcome(books, warnings);
    }

    private static string CreateFailureWarning(string phase, string sourceId) =>
        $"{phase}: source '{sourceId}' failed; retry later.";
}
