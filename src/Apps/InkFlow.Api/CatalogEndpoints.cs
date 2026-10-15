using InkFlow.Modules.Content.Application;
using InkFlow.Modules.Crawling.Application;

namespace InkFlow.Api;

public static class CatalogEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/books", async (CatalogQueryService catalog, CancellationToken ct) =>
        {
            var books = await catalog.ListBooksAsync(ct);
            return Results.Ok(books);
        });

        // 来源搜索发现:幂等导入 + v1 匹配后返回归并结果(落库数据)。
        api.MapGet("/search", async (
            string q,
            BookDiscoveryService discovery,
            IContentPolicyReader policy,
            CancellationToken ct) =>
        {
            var outcome = await discovery.DiscoverAsync(q ?? string.Empty, ct);
            var visibleBooks = new List<DiscoveredBook>(outcome.Books.Count);
            foreach (var book in outcome.Books)
            {
                if (!await policy.IsTakedownAsync(book.CanonicalBookId, ct))
                {
                    visibleBooks.Add(book);
                }
            }

            return Results.Ok(new { books = visibleBooks, warnings = outcome.Warnings });
        });

        api.MapGet("/books/{bookId:guid}", async (Guid bookId, CatalogQueryService catalog, CancellationToken ct) =>
        {
            var book = await catalog.GetBookAsync(bookId, ct);
            return book is null ? Results.NotFound() : Results.Ok(book);
        });

        api.MapGet("/chapters/{chapterId:guid}/content",
            async (Guid chapterId, CatalogQueryService catalog, CancellationToken ct) =>
        {
            var content = await catalog.GetChapterContentAsync(chapterId, ct);
            return content is null ? Results.NotFound() : Results.Ok(content);
        });
    }
}
