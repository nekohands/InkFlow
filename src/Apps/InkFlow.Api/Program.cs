// Public Content API:目录/内容端点只读(数据来自已落库正典数据),
// /search 端点是唯一的写侧入口——触发来源发现(幂等导入+匹配),随后仍从落库数据返回。
using InkFlow.Api;
using InkFlow.BuildingBlocks.Observability;
using InkFlow.BuildingBlocks.Security;
using InkFlow.Modules.Content.Application;
using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Legado.Application;
using InkFlow.Modules.Library.Application;
using InkFlow.Modules.Library.Domain;
using InkFlow.Modules.Library.Infrastructure.Persistence;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using InkFlow.Modules.Sources.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddInkFlowObservability("InkFlow.Api");
builder.Services.AddInkFlowApiRateLimiting(
    ApiRateLimitOptions.FromConfiguration(builder.Configuration));
builder.Services.AddSingleton<IAuditEventSink, LoggingAuditEventSink>();

// 来源发现按需使用老站编码(kanunu8 GB18030 等)。
System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

var databaseConnectionString =
    builder.Configuration.GetConnectionString("Database")
    ?? "Host=localhost;Port=5432;Database=inkflow;Username=inkflow;Password=inkflow";

builder.Services.AddDbContext<LibraryDbContext>(options =>
    options.UseNpgsql(databaseConnectionString));
builder.Services.AddDbContext<InkFlow.Modules.Sources.Infrastructure.Persistence.SourcesDbContext>(options =>
    options.UseNpgsql(databaseConnectionString));

builder.Services.AddScoped<ICanonicalBookRepository, EfCanonicalBookRepository>();
builder.Services.AddScoped<IMatchCandidateRepository, InkFlow.Modules.Library.Infrastructure.Persistence.EfMatchCandidateRepository>();
builder.Services.AddScoped<ISourceRepository, InkFlow.Modules.Sources.Infrastructure.Persistence.EfSourceRepository>();
builder.Services.AddScoped<ISourceBookRepository, InkFlow.Modules.Sources.Infrastructure.Persistence.EfSourceBookRepository>();
builder.Services.AddScoped<ISourceHealthRepository, InkFlow.Modules.Sources.Infrastructure.Persistence.EfSourceHealthRepository>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<SourceHealthService>();
builder.Services.AddScoped<ISourceHealthReader>(sp => sp.GetRequiredService<SourceHealthService>());

// 规则型/代码型适配器组合根(与 Worker 同源):健康感知由 BookDiscoveryService 内部执行。
builder.Services.AddSingleton<IIpAddressResolver, DnsIpAddressResolver>();
builder.Services.AddHttpClient<ISourceHttpClient, ProductionSafeSourceHttpClient>();
builder.Services.AddHttpClient<InkFlow.Sources.Adapters.Kanunu8.KanunuSourceAdapter>();
builder.Services.AddScoped<ISelectorEvaluator, CssSelectorEvaluator>();
builder.Services.AddScoped<RuleAdapter>();
builder.Services.AddScoped<ISourceAdapterFactory>(sp => new SourceAdapterFactory(
    sp.GetRequiredService<ISourceRepository>(),
    sp.GetRequiredService<RuleAdapter>(),
    sp.GetRequiredService<ISelectorEvaluator>(),
    [sp.GetRequiredService<InkFlow.Sources.Adapters.Kanunu8.KanunuSourceAdapter>()]));
builder.Services.AddScoped<SourceCatalogService>();
builder.Services.AddScoped<CanonicalBookMatchingService>();
builder.Services.AddScoped<BookDiscoveryService>();

var connectionStringForContent =
    builder.Configuration.GetConnectionString("Database")
    ?? "Host=localhost;Port=5432;Database=inkflow;Username=inkflow;Password=inkflow";
builder.Services.AddScoped<InkFlow.Modules.Content.Infrastructure.Persistence.ContentDbContext>(_ =>
    new InkFlow.Modules.Content.Infrastructure.Persistence.ContentDbContext(
        new DbContextOptionsBuilder<InkFlow.Modules.Content.Infrastructure.Persistence.ContentDbContext>()
            .UseNpgsql(connectionStringForContent)
            .Options));
builder.Services.AddScoped<InkFlow.Modules.Content.Application.IContentVersionRepository,
    InkFlow.Modules.Content.Infrastructure.Persistence.EfContentVersionRepository>();
builder.Services.AddScoped<CatalogQueryService>();
builder.Services.AddScoped<LegadoContractService>();

var app = builder.Build();

// 审计包在限流包外层，确保 429 也进入请求轨迹；health 不进入业务审计。
app.UseMiddleware<RequestAuditMiddleware>();
app.UseRateLimiter();

app.MapGet("/health", () => Results.Json(new { status = "healthy", service = "InkFlow.Api" }));

var api = app.MapGroup("/api/v1")
    .RequireRateLimiting(ApiRateLimitPolicies.PublicPolicyName);

api.MapGet("/books", async (CatalogQueryService catalog, CancellationToken ct) =>
{
    var books = await catalog.ListBooksAsync(ct);
    return Results.Ok(books);
});

// 来源搜索发现:幂等导入 + v1 匹配后返回归并结果(落库数据)。
api.MapGet("/search", async (string q, BookDiscoveryService discovery, CancellationToken ct) =>
{
    var outcome = await discovery.DiscoverAsync(q ?? string.Empty, ct);
    return Results.Ok(new { books = outcome.Books, warnings = outcome.Warnings });
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

// ---- Minimal Web Reader(服务端渲染 HTML)----

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
            "<!DOCTYPE html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><title>未找到</title></head><body><main><p role=\"status\">该章节尚未发布内容。</p><p><a href=\"/reader\">返回书目</a></p></main></body></html>",
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
app.MapGet("/legado/book-source.json", (HttpContext http) =>
{
    var baseUrl = $"{http.Request.Scheme}://{http.Request.Host}";
    return Results.Text(
        LegadoBookSourceManifest.Generate(baseUrl),
        contentType: "application/json; charset=utf-8");
}).RequireRateLimiting(ApiRateLimitPolicies.LegadoPolicyName);

app.Run();

public partial class Program;
