using InkFlow.Modules.Content.Application;
using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Legado.Application;

namespace InkFlow.Api;

public static class ReaderEndpoints
{
    public static void Map(WebApplication app)
    {
        // ---- Web Reader / PWA(服务端渲染 HTML + 渐进增强)----

        app.MapGet("/reader/manifest.webmanifest", () =>
            Results.Text(
                ReaderHtml.PwaManifest(),
                contentType: "application/manifest+json; charset=utf-8"))
            .RequireRateLimiting(ApiRateLimitPolicies.PublicPolicyName);

        app.MapGet("/reader/sw.js", () =>
            Results.Text(
                ReaderHtml.ServiceWorker(),
                contentType: "application/javascript; charset=utf-8"))
            .RequireRateLimiting(ApiRateLimitPolicies.PublicPolicyName);

        app.MapGet("/reader/icon-192.svg", () =>
            Results.Text(ReaderHtml.PwaIcon(), contentType: "image/svg+xml; charset=utf-8"))
            .RequireRateLimiting(ApiRateLimitPolicies.PublicPolicyName);

        app.MapGet("/reader/icon-512.svg", () =>
            Results.Text(ReaderHtml.PwaIcon(), contentType: "image/svg+xml; charset=utf-8"))
            .RequireRateLimiting(ApiRateLimitPolicies.PublicPolicyName);

        app.MapGet("/reader/offline", () =>
            Results.Content(
                ReaderHtml.OfflinePage(),
                contentType: "text/html; charset=utf-8"))
            .RequireRateLimiting(ApiRateLimitPolicies.PublicPolicyName);

        app.MapGet("/reader/account", () =>
            Results.Content(
                ReaderHtml.AccountPage(),
                contentType: "text/html; charset=utf-8"))
            .RequireRateLimiting(ApiRateLimitPolicies.PublicPolicyName);

        app.MapGet("/reader/account/register", () =>
            Results.Content(
                ReaderHtml.AccountPage(registration: true),
                contentType: "text/html; charset=utf-8"))
            .RequireRateLimiting(ApiRateLimitPolicies.PublicPolicyName);

        app.MapGet("/reader/shelf", () =>
            Results.Content(
                ReaderHtml.ShelfPage(),
                contentType: "text/html; charset=utf-8"))
            .RequireRateLimiting(ApiRateLimitPolicies.PublicPolicyName);

        app.MapGet("/reader/history", () =>
            Results.Content(
                ReaderHtml.HistoryPage(),
                contentType: "text/html; charset=utf-8"))
            .RequireRateLimiting(ApiRateLimitPolicies.PublicPolicyName);

        // 运维页面本身只返回静态壳；快照与修复命令仍由前端通过受保护 API 获取。
        app.MapGet("/admin/operations", () =>
            Results.Content(
                ReaderHtml.OperationsPage(),
                contentType: "text/html; charset=utf-8"))
            .RequireRateLimiting(ApiRateLimitPolicies.PublicPolicyName);

        app.MapGet("/reader", async (string? q, CatalogQueryService catalog, BookDiscoveryService discovery, CancellationToken ct) =>
        {
            var query = q?.Trim() ?? string.Empty;
            var searched = query.Length > 0;

            // 非空搜索先走来源发现(幂等导入+匹配,失败隔离为逐源 warning);
            // 发现环节整体异常也不阻断页面——降级为"结果可能不完整"提示后仍从
            // 落库正典数据过滤返回(阅读路径零实时抓取)。
            var sourceDegraded = false;
            if (searched)
            {
                try
                {
                    var outcome = await discovery.DiscoverAsync(query, ct);
                    sourceDegraded = outcome.Warnings.Count > 0;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    sourceDegraded = true;
                }
            }

            var books = await catalog.SearchBooksAsync(query, ct);
            return Results.Content(
                ReaderHtml.BookListPage(books, searched ? query : null, searched, sourceDegraded),
                contentType: "text/html; charset=utf-8");
        }).RequireRateLimiting(ApiRateLimitPolicies.PublicPolicyName);

        app.MapGet("/reader/books/{bookId:guid}",
            async (Guid bookId, CatalogQueryService catalog, CancellationToken ct) =>
        {
            var book = await catalog.GetBookAsync(bookId, ct);
            return book is null
                ? Results.Content(ReaderHtml.BookListPage([], null), "text/html; charset=utf-8", statusCode: 404)
                : Results.Content(
                    ReaderHtml.BookDetailPage(book), contentType: "text/html; charset=utf-8");
        });

        app.MapGet("/reader/read/{chapterId:guid}",
            async (Guid chapterId, CatalogQueryService catalog, CancellationToken ct) =>
        {
            var content = await catalog.GetChapterContentAsync(chapterId, ct);
            if (content is null)
            {
                return Results.Content(
                    ReaderHtml.MissingChapterPage(),
                    "text/html; charset=utf-8",
                    statusCode: 404);
            }

            var book = await catalog.GetBookAsync(content.BookId, ct);
            var chapters = book?.Chapters ?? [];

            (Guid ChapterId, string Title)? previous = null;
            (Guid ChapterId, string Title)? next = null;
            for (var i = 0; i < chapters.Count; i++)
            {
                if (chapters[i].ChapterId != chapterId)
                {
                    continue;
                }

                previous = i > 0 ? (chapters[i - 1].ChapterId, chapters[i - 1].Title) : null;
                next = i + 1 < chapters.Count ? (chapters[i + 1].ChapterId, chapters[i + 1].Title) : null;
                break;
            }

            return Results.Content(
                ReaderHtml.ChapterPage(content, previous, next, content.BookId, book?.Title ?? string.Empty),
                contentType: "text/html; charset=utf-8");
        });

        // 书源清单:由代码生成,baseUrl 取请求自身的 scheme+host。
        app.MapGet("/legado/book-source.json", (HttpContext http, ILegadoRuleGenerator legadoRuleGenerator) =>
        {
            var baseUrl = $"{http.Request.Scheme}://{http.Request.Host}";
            return Results.Text(
                legadoRuleGenerator.Generate(baseUrl),
                contentType: "application/json; charset=utf-8");
        }).RequireRateLimiting(ApiRateLimitPolicies.LegadoPolicyName);
    }
}
