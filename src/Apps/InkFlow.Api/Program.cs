// Public Content API:全部端点只读,数据来自已落库的正典书目与 IsCurrent 内容版本——
// 普通阅读路径零实时抓取(架构不变量 3)。
using InkFlow.Modules.Content.Application;
using InkFlow.Modules.Legado.Application;
using InkFlow.Modules.Library.Application;
using InkFlow.Modules.Library.Domain;
using InkFlow.Modules.Library.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<LibraryDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Database")
        ?? "Host=localhost;Port=5432;Database=inkflow;Username=inkflow;Password=inkflow"));

builder.Services.AddScoped<ICanonicalBookRepository, EfCanonicalBookRepository>();
builder.Services.AddSingleton(TimeProvider.System);

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

app.MapGet("/health", () => Results.Json(new { status = "healthy", service = "InkFlow.Api" }));

var api = app.MapGroup("/api/v1");

api.MapGet("/books", async (CatalogQueryService catalog, CancellationToken ct) =>
{
    var books = await catalog.ListBooksAsync(ct);
    return Results.Ok(books);
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

var legado = app.MapGroup("/api/legado/v1");

legado.MapGet("/search", async (string q, LegadoContractService legadoService, CancellationToken ct) =>
{
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

app.MapGet("/reader", async (string? q, CatalogQueryService catalog, CancellationToken ct) =>
{
    var books = await catalog.ListBooksAsync(ct);
    return Results.Content(
        ReaderHtml.BookListPage(books, q), contentType: "text/html; charset=utf-8");
});

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
});

app.Run();

public partial class Program;
