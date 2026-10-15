using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Legado.Application;
using InkFlow.Modules.Identity.Application;

namespace InkFlow.Api;

public static class LegadoEndpoints
{
    public static void Map(WebApplication app)
    {
        // ---- Legado v1 契约(阅读 3.0 接入)----

        var legado = app.MapGroup("/api/legado/v1")
            .RequireRateLimiting(ApiRateLimitPolicies.LegadoPolicyName);

        legado.MapGet("/search", async (string q, BookDiscoveryService discovery, LegadoContractService legadoService, CancellationToken ct) =>
        {
            // 先经来源发现把命中的新书幂等导入并匹配正典身份,再从落库数据返回
            // Legado DTO——契约形态保持稳定,冷启动搜索从此可发现未入库书目。
            await discovery.DiscoverAsync(q ?? string.Empty, ct);
            var results = await legadoService.SearchAsync(q ?? string.Empty, ct);
            return Results.Json(new { data = results });
        });

        legado.MapGet("/books/{bookId:guid}", async (Guid bookId, LegadoContractService legadoService, CancellationToken ct) =>
        {
            var book = await legadoService.GetBookAsync(bookId, ct);
            return book is null ? Results.NotFound() : Results.Json(book);
        });

        legado.MapGet("/books/{bookId:guid}/chapters",
            async (Guid bookId, LegadoContractService legadoService, CancellationToken ct) =>
        {
            var toc = await legadoService.GetTocAsync(bookId, ct);
            return toc is null ? Results.NotFound() : Results.Json(new { data = toc });
        });

        legado.MapGet("/chapters/{chapterId:guid}",
            async (Guid chapterId, LegadoContractService legadoService, CancellationToken ct) =>
        {
            var content = await legadoService.GetChapterContentAsync(chapterId, ct);
            return content is null ? Results.NotFound() : Results.Json(content);
        });

        // ---- Personal Legado v1 契约(通过 X-InkFlow-Legado-Token 认证)----

        var personalLegado = app.MapGroup(LegadoRoutePrefixes.Personal)
            .RequireRateLimiting(ApiRateLimitPolicies.LegadoPolicyName)
            .RequireAuthorization(IdentityPolicies.LegadoRead);

        personalLegado.MapGet("/search", async (
            string q,
            BookDiscoveryService discovery,
            LegadoContractService legadoService,
            CancellationToken ct) =>
        {
            await discovery.DiscoverAsync(q ?? string.Empty, ct);
            var results = await legadoService
                .SearchAsync(q ?? string.Empty, LegadoRoutePrefixes.Personal, ct)
                .ConfigureAwait(false);
            return Results.Json(new { data = results });
        });

        personalLegado.MapGet("/books/{bookId:guid}", async (
            Guid bookId,
            LegadoContractService legadoService,
            CancellationToken ct) =>
        {
            var book = await legadoService
                .GetBookAsync(bookId, LegadoRoutePrefixes.Personal, ct)
                .ConfigureAwait(false);
            return book is null ? Results.NotFound() : Results.Json(book);
        });

        personalLegado.MapGet("/books/{bookId:guid}/chapters", async (
            Guid bookId,
            LegadoContractService legadoService,
            CancellationToken ct) =>
        {
            var toc = await legadoService
                .GetTocAsync(bookId, LegadoRoutePrefixes.Personal, ct)
                .ConfigureAwait(false);
            return toc is null ? Results.NotFound() : Results.Json(new { data = toc });
        });

        personalLegado.MapGet("/chapters/{chapterId:guid}", async (
            Guid chapterId,
            LegadoContractService legadoService,
            CancellationToken ct) =>
        {
            var content = await legadoService
                .GetChapterContentAsync(chapterId, LegadoRoutePrefixes.Personal, ct)
                .ConfigureAwait(false);
            return content is null ? Results.NotFound() : Results.Json(content);
        });
    }
}
