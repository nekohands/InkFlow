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
