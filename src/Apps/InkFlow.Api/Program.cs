// Public Content API:全部端点只读,数据来自已落库的正典书目与 IsCurrent 内容版本——
// 普通阅读路径零实时抓取(架构不变量 3)。
using InkFlow.Modules.Content.Application;
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

app.Run();

public partial class Program;
