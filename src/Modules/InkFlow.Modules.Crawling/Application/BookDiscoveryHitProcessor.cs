using InkFlow.Modules.Library.Application;
using InkFlow.Modules.Library.Domain;
using InkFlow.Modules.Sources.Application;

namespace InkFlow.Modules.Crawling.Application;

internal sealed record BookDiscoveryHitResult(
    CanonicalBook? Book,
    bool NewlyCreated,
    IReadOnlyList<string> Warnings)
{
    public bool IsSuccess => Book is not null;

    public static BookDiscoveryHitResult Failure(IReadOnlyList<string> warnings) =>
        new(null, false, warnings);

    public static BookDiscoveryHitResult Success(CanonicalBook book, bool newlyCreated) =>
        new(book, newlyCreated, []);
}

/// <summary>将单个搜索命中导入来源书目并归并到正典书。</summary>
internal sealed class BookDiscoveryHitProcessor(
    SourceCatalogService catalog,
    CanonicalBookMatchingService matching)
{
    public async Task<BookDiscoveryHitResult> ProcessAsync(
        string sourceId,
        SourceSearchResult hit,
        CancellationToken cancellationToken)
    {
        var import = await catalog
            .ImportBookInfoAsync(sourceId, hit.ExternalBookId, cancellationToken)
            .ConfigureAwait(false);

        if (!import.IsSuccess || import.Book is null)
        {
            return BookDiscoveryHitResult.Failure([
                $"import: source '{sourceId}' book '{hit.ExternalBookId}': " +
                string.Join("; ", import.Errors),
            ]);
        }

        var match = await matching
            .CreateOrMatchAsync(sourceId, hit.ExternalBookId, cancellationToken)
            .ConfigureAwait(false);

        if (!match.IsSuccess || match.Book is null)
        {
            return BookDiscoveryHitResult.Failure([
                $"match: source '{sourceId}' book '{hit.ExternalBookId}': " +
                string.Join("; ", match.Errors),
            ]);
        }

        return BookDiscoveryHitResult.Success(match.Book, match.NewlyCreated);
    }
}
