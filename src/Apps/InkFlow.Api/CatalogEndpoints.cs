using InkFlow.BuildingBlocks.Persistence;
using InkFlow.Modules.Legado;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Api;

public static class CatalogEndpoints
{
    public static WebApplication MapCatalogEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/search", SearchBooksAsync);
        app.MapGet("/api/v1/books/{bookId:guid}", GetBookAsync);
        app.MapGet("/api/v1/books/{bookId:guid}/chapters", GetChaptersAsync);
        app.MapGet("/api/v1/chapters/{chapterId:guid}", GetChapterContentAsync);

        app.MapGet("/api/legado/v1/search", SearchLegadoAsync);
        app.MapGet("/api/legado/v1/books/{bookId:guid}", GetLegadoBookAsync);
        app.MapGet("/api/legado/v1/books/{bookId:guid}/chapters", GetLegadoChaptersAsync);
        app.MapGet("/api/legado/v1/chapters/{chapterId:guid}", GetLegadoContentAsync);
        app.MapGet("/legado/book-source.json", (HttpRequest request) =>
        {
            var generator = new LegadoBookSourceGenerator();
            return Results.Json(generator.Generate(GetPublicBaseUri(request)));
        });

        return app;
    }

    private static async Task<IResult> SearchBooksAsync(string? q, LibraryDbContext library, CancellationToken cancellationToken)
    {
        var query = library.Books.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var pattern = $"%{EscapeLike(q.Trim())}%";
            query = query.Where(book => EF.Functions.ILike(book.Title, pattern, "\\") || EF.Functions.ILike(book.Author, pattern, "\\"));
        }

        var books = await query.OrderByDescending(book => book.UpdatedAtUtc).Take(50)
            .Select(book => new BookSummaryDto(book.Id, book.Title, book.Author, book.Description, book.Revision))
            .ToListAsync(cancellationToken);
        return Results.Ok(books);
    }

    private static async Task<IResult> GetBookAsync(Guid bookId, LibraryDbContext library, CancellationToken cancellationToken)
    {
        var book = await library.Books.AsNoTracking().SingleOrDefaultAsync(item => item.Id == bookId, cancellationToken);
        if (book is null)
        {
            return Results.NotFound();
        }

        var latest = await library.Chapters.AsNoTracking().Where(chapter => chapter.BookId == bookId)
            .OrderByDescending(chapter => chapter.Sequence).Select(chapter => chapter.Title).FirstOrDefaultAsync(cancellationToken);
        return Results.Ok(new BookDetailDto(book.Id, book.Title, book.Author, book.Description, book.Status, latest, book.Revision));
    }

    private static async Task<IResult> GetChaptersAsync(Guid bookId, LibraryDbContext library, CancellationToken cancellationToken)
    {
        var exists = await library.Books.AsNoTracking().AnyAsync(book => book.Id == bookId, cancellationToken);
        if (!exists)
        {
            return Results.NotFound();
        }

        var chapters = await library.Chapters.AsNoTracking().Where(chapter => chapter.BookId == bookId)
            .OrderBy(chapter => chapter.Sequence)
            .Select(chapter => new ChapterSummaryDto(chapter.Id, chapter.Title, chapter.Sequence, chapter.DisplayNumber, chapter.Revision))
            .ToListAsync(cancellationToken);
        return Results.Ok(chapters);
    }

    private static async Task<IResult> GetChapterContentAsync(
        Guid chapterId,
        LibraryDbContext library,
        ContentDbContext content,
        CancellationToken cancellationToken)
    {
        var chapter = await library.Chapters.AsNoTracking().SingleOrDefaultAsync(item => item.Id == chapterId, cancellationToken);
        if (chapter is null)
        {
            return Results.NotFound();
        }

        var selection = await content.ChapterSelections.AsNoTracking().SingleOrDefaultAsync(item => item.ChapterId == chapterId, cancellationToken);
        if (selection is null)
        {
            return ContentNotReady(chapterId);
        }

        var version = await content.ContentVersions.AsNoTracking().SingleOrDefaultAsync(item => item.Id == selection.ContentVersionId, cancellationToken);
        if (version is null)
        {
            return ContentNotReady(chapterId);
        }

        var blob = await content.ContentBlobs.AsNoTracking().SingleOrDefaultAsync(item => item.Id == version.BlobId, cancellationToken);
        if (blob?.InlineContent is null)
        {
            return ContentNotReady(chapterId);
        }

        return Results.Ok(new ChapterContentDto(chapter.Id, chapter.Title, blob.InlineContent, version.Id, version.QualityScore, version.CanonicalHash));
    }

    private static async Task<IResult> SearchLegadoAsync(
        string? q,
        HttpRequest request,
        LibraryDbContext library,
        CancellationToken cancellationToken)
    {
        var pattern = $"%{EscapeLike((q ?? string.Empty).Trim())}%";
        var books = await library.Books.AsNoTracking()
            .Where(book => q == null || q == string.Empty || EF.Functions.ILike(book.Title, pattern, "\\") || EF.Functions.ILike(book.Author, pattern, "\\"))
            .OrderByDescending(book => book.UpdatedAtUtc).Take(50).ToListAsync(cancellationToken);
        var baseUrl = GetPublicBaseUri(request).GetLeftPart(UriPartial.Authority).TrimEnd('/');
        var result = new List<LegadoSearchBook>(books.Count);
        foreach (var book in books)
        {
            var lastChapter = await library.Chapters.AsNoTracking().Where(chapter => chapter.BookId == book.Id)
                .OrderByDescending(chapter => chapter.Sequence).Select(chapter => chapter.Title).FirstOrDefaultAsync(cancellationToken);
            result.Add(new(book.Title, book.Author, $"{baseUrl}/api/legado/v1/books/{book.Id}", null, book.Description, lastChapter));
        }
        return Results.Ok(result);
    }

    private static async Task<IResult> GetLegadoBookAsync(Guid bookId, HttpRequest request, LibraryDbContext library, CancellationToken cancellationToken)
    {
        var book = await library.Books.AsNoTracking().SingleOrDefaultAsync(item => item.Id == bookId, cancellationToken);
        if (book is null)
        {
            return Results.NotFound();
        }
        var lastChapter = await library.Chapters.AsNoTracking().Where(chapter => chapter.BookId == bookId)
            .OrderByDescending(chapter => chapter.Sequence).Select(chapter => chapter.Title).FirstOrDefaultAsync(cancellationToken);
        var baseUrl = GetPublicBaseUri(request).GetLeftPart(UriPartial.Authority).TrimEnd('/');
        return Results.Ok(new LegadoBookInfo(
            book.Title,
            book.Author,
            $"{baseUrl}/api/legado/v1/books/{book.Id}",
            $"{baseUrl}/api/legado/v1/books/{book.Id}/chapters",
            null,
            book.Description,
            lastChapter));
    }

    private static async Task<IResult> GetLegadoChaptersAsync(Guid bookId, HttpRequest request, LibraryDbContext library, CancellationToken cancellationToken)
    {
        if (!await library.Books.AsNoTracking().AnyAsync(book => book.Id == bookId, cancellationToken))
        {
            return Results.NotFound();
        }
        var baseUrl = GetPublicBaseUri(request).GetLeftPart(UriPartial.Authority).TrimEnd('/');
        var chapters = await library.Chapters.AsNoTracking().Where(chapter => chapter.BookId == bookId).OrderBy(chapter => chapter.Sequence).ToListAsync(cancellationToken);
        return Results.Ok(chapters.Select(chapter => new LegadoChapter(chapter.Title, $"{baseUrl}/api/legado/v1/chapters/{chapter.Id}", chapter.Sequence)));
    }

    private static async Task<IResult> GetLegadoContentAsync(Guid chapterId, LibraryDbContext library, ContentDbContext content, CancellationToken cancellationToken)
    {
        var chapter = await library.Chapters.AsNoTracking().SingleOrDefaultAsync(item => item.Id == chapterId, cancellationToken);
        if (chapter is null)
        {
            return Results.NotFound();
        }
        var selection = await content.ChapterSelections.AsNoTracking().SingleOrDefaultAsync(item => item.ChapterId == chapterId, cancellationToken);
        if (selection is null)
        {
            return ContentNotReady(chapterId);
        }
        var version = await content.ContentVersions.AsNoTracking().SingleOrDefaultAsync(item => item.Id == selection.ContentVersionId, cancellationToken);
        var blob = version is null ? null : await content.ContentBlobs.AsNoTracking().SingleOrDefaultAsync(item => item.Id == version.BlobId, cancellationToken);
        return blob?.InlineContent is null ? ContentNotReady(chapterId) : Results.Ok(new LegadoContent(blob.InlineContent));
    }

    private static IResult ContentNotReady(Guid chapterId) => Results.Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "Chapter content is not ready",
        type: "https://inkflow/errors/content-not-ready",
        extensions: new Dictionary<string, object?> { ["code"] = "CONTENT_NOT_READY", ["chapterId"] = chapterId });

    private static Uri GetPublicBaseUri(HttpRequest request) => new($"{request.Scheme}://{request.Host}");

    private static string EscapeLike(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);

    private sealed record BookSummaryDto(Guid Id, string Title, string Author, string? Description, long Revision);
    private sealed record BookDetailDto(Guid Id, string Title, string Author, string? Description, string Status, string? LatestChapter, long Revision);
    private sealed record ChapterSummaryDto(Guid Id, string Title, long Sequence, int? DisplayNumber, long Revision);
    private sealed record ChapterContentDto(Guid ChapterId, string Title, string Content, Guid ContentVersionId, double QualityScore, string ContentHash);
}
